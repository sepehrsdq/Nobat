namespace Nobat.Domain.Common;

/// <summary>
/// Raised when a domain invariant is violated. The <see cref="Code"/> is a stable,
/// client-safe identifier (for example <c>appointment.slot_taken</c>) that maps to ProblemDetails.
/// </summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
