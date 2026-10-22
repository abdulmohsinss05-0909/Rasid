using Rasid.Application.Common.Results;

namespace Rasid.Application.Common.Messaging;

/// <summary>A use-case request that produces <typeparamref name="TResponse"/>.</summary>
#pragma warning disable CA1040 // Marker interfaces carry the response type for the dispatcher.
public interface IRequest<TResponse>;

/// <summary>Changes state (write side).</summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>Reads state without side effects (read side).</summary>
public interface IQuery<TResponse> : IRequest<TResponse>;
#pragma warning restore CA1040

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Dispatches a request to its handler after running its validators.</summary>
public interface ISender
{
    Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
