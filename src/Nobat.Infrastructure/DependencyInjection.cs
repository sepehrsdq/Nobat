using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nobat.Application.Abstractions;
using Nobat.Infrastructure.Persistence;
using Nobat.Infrastructure.Persistence.Interceptors;
using Nobat.Infrastructure.Security;
using Nobat.Infrastructure.Tenancy;

namespace Nobat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<TenantIdInterceptor>();
        services.AddScoped<ITenantResolver, TenantResolver>();

        services.AddDbContext<NobatDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(typeof(NobatDbContext).Assembly.GetName().Name);
                sqlServer.MigrationsHistoryTable("__ef_migrations_history");
            });

            options.AddInterceptors(provider.GetRequiredService<TenantIdInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<NobatDbContext>());

        services.TryAddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
