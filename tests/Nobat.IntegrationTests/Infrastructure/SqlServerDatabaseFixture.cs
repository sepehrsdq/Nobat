using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nobat.Infrastructure.Persistence;
using Nobat.Infrastructure.Tenancy;

namespace Nobat.IntegrationTests.Infrastructure;

/// <summary>
/// Creates a throwaway SQL Server database for the test run and drops it afterwards.
/// The server comes from <c>NOBAT_TEST_SQL</c> when set (CI service container), otherwise
/// from LocalDB on the developer machine. Tests never touch the development database.
/// </summary>
public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private const string DefaultServerConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

    private string _baseConnectionString = string.Empty;
    private string _databaseName = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("NOBAT_TEST_SQL") is { Length: > 0 } configured
            ? configured
            : DefaultServerConnectionString;

        _databaseName = "NobatTests_" + Guid.NewGuid().ToString("N");

        await ExecuteOnMasterAsync($"CREATE DATABASE [{_databaseName}]");

        ConnectionString = new SqlConnectionStringBuilder(_baseConnectionString)
        {
            InitialCatalog = _databaseName,
        }.ConnectionString;

        await using var db = TestDb.Create(ConnectionString, tenantId: null);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_databaseName))
        {
            return;
        }

        await ExecuteOnMasterAsync($"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
        await ExecuteOnMasterAsync($"DROP DATABASE [{_databaseName}]");
    }

    private async Task ExecuteOnMasterAsync(string sql)
    {
        await using var connection = await OpenMasterAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Opens a master connection, waiting for the server to accept connections.
    /// A CI service container or a cold LocalDB instance is not ready the instant the job starts.
    /// </summary>
    private async Task<SqlConnection> OpenMasterAsync()
    {
        const int maxAttempts = 20;
        var builder = new SqlConnectionStringBuilder(_baseConnectionString) { InitialCatalog = "master" };
        SqlException? lastFailure = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var connection = new SqlConnection(builder.ConnectionString);

            try
            {
                await connection.OpenAsync();
                return connection;
            }
            catch (SqlException exception)
            {
                lastFailure = exception;
                await connection.DisposeAsync();

                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
            }
        }

        throw new InvalidOperationException(
            "Integration tests need a SQL Server instance. Install SQL Server Express LocalDB " +
            "or point NOBAT_TEST_SQL at a reachable server.",
            lastFailure);
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerDatabaseFixture>
{
    public const string Name = "sqlserver";
}
