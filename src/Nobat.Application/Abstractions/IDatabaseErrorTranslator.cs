namespace Nobat.Application.Abstractions;

/// <summary>
/// Keeps provider-specific database error codes out of Application. The only implementation
/// today speaks SQL Server; a different provider only has to replace this class.
/// </summary>
public interface IDatabaseErrorTranslator
{
    /// <summary>True when the failure came from a unique index or primary key.</summary>
    bool IsUniqueViolation(Exception exception);
}
