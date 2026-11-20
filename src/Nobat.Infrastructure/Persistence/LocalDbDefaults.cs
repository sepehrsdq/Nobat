namespace Nobat.Infrastructure.Persistence;

/// <summary>
/// Local development database. LocalDB runs in user mode, needs no service and no password,
/// which is why the laptop uses it instead of a Docker image.
/// </summary>
public static class LocalDbDefaults
{
    public const string Server = @"(localdb)\MSSQLLocalDB";

    public const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=Nobat;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
}
