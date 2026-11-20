using Microsoft.Data.SqlClient;
using Nobat.Application.Abstractions;

namespace Nobat.Infrastructure.Persistence;

/// <summary>
/// SQL Server signals a duplicate key with 2601 (unique index) and 2627 (unique constraint).
/// Both mean the same thing for us: someone else took the row first.
/// </summary>
public sealed class SqlServerErrorTranslator : IDatabaseErrorTranslator
{
    private const int DuplicateKeyRow = 2601;
    private const int UniqueConstraintViolation = 2627;

    public bool IsUniqueViolation(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number is DuplicateKeyRow or UniqueConstraintViolation)
            {
                return true;
            }
        }

        return false;
    }
}
