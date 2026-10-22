using Rasid.Application.Common.Results;

namespace Rasid.Api.Common;

/// <summary>Maps application results to HTTP responses; every failure becomes an RFC 9457 ProblemDetails.</summary>
internal static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsSuccess
            ? onSuccess?.Invoke(result.Value) ?? TypedResults.Ok(result.Value)
            : result.Error.ToProblem();

    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Type == ErrorType.Validation && error.ValidationErrors is not null)
        {
            return TypedResults.ValidationProblem(
                error.ValidationErrors.ToDictionary(kv => kv.Key, kv => kv.Value),
                title: error.Message,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }

        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(
            statusCode: status,
            title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
