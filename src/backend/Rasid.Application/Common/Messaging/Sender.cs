using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Rasid.Application.Common.Results;

namespace Rasid.Application.Common.Messaging;

/// <summary>
/// Minimal in-house dispatcher (see ADR-010): resolves the handler for a request,
/// runs every registered FluentValidation validator first and short-circuits with a validation error.
/// </summary>
internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> Pipelines = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pipeline = (RequestPipeline<TResponse>)Pipelines.GetOrAdd(
            request.GetType(),
            static requestType => Activator.CreateInstance(
                typeof(RequestPipeline<,>).MakeGenericType(requestType, typeof(TResponse)))!);

        return pipeline.HandleAsync(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestPipeline<TResponse>
    {
        public abstract Task<Result<TResponse>> HandleAsync(
            IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken);
    }

    private sealed class RequestPipeline<TRequest, TResponse> : RequestPipeline<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override async Task<Result<TResponse>> HandleAsync(
            IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;

            var validators = services.GetServices<IValidator<TRequest>>().ToArray();
            if (validators.Length > 0)
            {
                var context = new ValidationContext<TRequest>(typedRequest);
                var failures = new List<FluentValidation.Results.ValidationFailure>();
                foreach (var validator in validators)
                {
                    var result = await validator.ValidateAsync(context, cancellationToken);
                    failures.AddRange(result.Errors);
                }

                if (failures.Count > 0)
                {
                    var errors = failures
                        .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray(), StringComparer.Ordinal);
                    return Error.Validation(errors);
                }
            }

            var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            return await handler.HandleAsync(typedRequest, cancellationToken);
        }
    }
}
