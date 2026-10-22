using System.Diagnostics.CodeAnalysis;

namespace Rasid.Application.Common.Results;

/// <summary>Represents "no value" for commands that only succeed or fail.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}

/// <summary>Outcome of a use case: a value or an <see cref="Error"/>. Never both.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

#pragma warning disable CA1000 // Static factory members on generic types are the idiomatic API for Result<T>.
    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
