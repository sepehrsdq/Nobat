using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;
using Nobat.Domain.Users;

namespace Nobat.Application.Auth;

public sealed record LoginRequest(string TenantSlug, string Phone, string Password);

/// <summary>
/// Signs a panel user in. Every failure returns the same code and message so the endpoint
/// cannot be used to find out whether a phone number exists.
/// </summary>
public sealed class LoginHandler
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContextSetter _tenantContext;

    public LoginHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ITenantContextSetter tenantContext)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
    }

    public async Task<AuthSession> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slug = (request.TenantSlug ?? string.Empty).Trim().ToLowerInvariant();

        var tenant = slug.Length == 0
            ? null
            : await _db.Tenants.FirstOrDefaultAsync(candidate => candidate.Slug == slug, cancellationToken);

        if (tenant is null || !tenant.IsActive)
        {
            throw InvalidCredentials();
        }

        // From here on every query is tenant-filtered.
        _tenantContext.SetTenant(tenant.Id);

        if (!IranianPhone.TryNormalize(request.Phone, out var phone))
        {
            _passwordHasher.Hash(request.Password ?? string.Empty);
            throw InvalidCredentials();
        }

        var user = await _db.Users.FirstOrDefaultAsync(candidate => candidate.PhoneNumber == phone, cancellationToken);

        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            // Burn the same amount of work as a real verification.
            _passwordHasher.Hash(request.Password ?? string.Empty);
            throw InvalidCredentials();
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password ?? string.Empty))
        {
            throw InvalidCredentials();
        }

        return new AuthSession(user.Id, tenant.Id, tenant.Slug, user.FullName, user.Role);
    }

    private static DomainRuleViolationException InvalidCredentials() => new(
        "auth.invalid_credentials",
        "شمارهٔ موبایل یا رمز عبور درست نیست.");
}
