using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nobat.Infrastructure.Tenancy;

namespace Nobat.Infrastructure.Persistence;

/// <summary>
/// Used only by <c>dotnet ef</c>. Migrations are generated manually, never applied at startup.
/// </summary>
public sealed class DesignTimeNobatDbContextFactory : IDesignTimeDbContextFactory<NobatDbContext>
{
    public NobatDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = LocalDbDefaults.ConnectionString;
        }

        var options = new DbContextOptionsBuilder<NobatDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NobatDbContext(options, new UnresolvedTenantContext());
    }
}
