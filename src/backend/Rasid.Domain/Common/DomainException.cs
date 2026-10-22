namespace Rasid.Domain.Common;

/// <summary>
/// Raised when a domain invariant would be violated. Input is validated before it reaches the domain,
/// so this exception signals a programming error or a bypassed validation path.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
