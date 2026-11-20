using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nobat.IntegrationTests.Infrastructure;

internal sealed class NobatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public NobatApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
    }
}
