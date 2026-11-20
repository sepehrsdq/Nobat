using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nobat.IntegrationTests.Infrastructure;

internal sealed class NobatApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public NobatApiFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        _connectionString = connectionString;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);

        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
