namespace Nobat.Infrastructure.Persistence;

/// <summary>
/// Thrown when code tries to read or write across tenants. It is a programming error,
/// not a user error, so it never turns into a 4xx response.
/// </summary>
public sealed class TenantIsolationException : Exception
{
    public TenantIsolationException(string message)
        : base(message)
    {
    }
}
