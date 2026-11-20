using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;
using Nobat.Domain.Tenants;
using Nobat.Domain.Users;

namespace Nobat.Application.Auth;

public sealed record RegisterClinicRequest(
    string Slug,
    string ClinicName,
    string OwnerName,
    string Phone,
    string Password);

/// <summary>
/// Creates a clinic and its first owner in one go. The tenant row is written first (it has no
/// <c>TenantId</c>), then the tenant context is set so the interceptor accepts the owner row.
/// </summary>
public sealed class RegisterClinicHandler
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContextSetter _tenantContext;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly TimeProvider _clock;

    public RegisterClinicHandler(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ITenantContextSetter tenantContext,
        IOptions<AuthOptions> authOptions,
        TimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _authOptions = authOptions;
        _clock = clock;
    }

    public async Task<AuthSession> HandleAsync(RegisterClinicRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_authOptions.Value.AllowPublicSignup)
        {
            throw new DomainRuleViolationException(
                "auth.signup_closed",
                "ثبت‌نام مطب در این نسخه بسته است. برای دمو از حساب آزمایشی استفاده کنید.");
        }

        PasswordPolicy.EnsureValid(request.Password);

        var now = _clock.GetUtcNow();
        var tenant = Tenant.Create(request.Slug, request.ClinicName, now);

        // Build (and validate) the owner before touching the database, so a bad phone or name
        // cannot leave a clinic without an owner.
        var owner = User.Create(tenant.Id, request.OwnerName, request.Phone, UserRole.Owner, now);
        owner.SetPasswordHash(_passwordHasher.Hash(request.Password));

        if (await _db.Tenants.AnyAsync(existing => existing.Slug == tenant.Slug, cancellationToken))
        {
            throw SlugTaken();
        }

        _db.Tenants.Add(tenant);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two signups raced on the same slug; the unique index decided.
            throw SlugTaken();
        }

        _tenantContext.SetTenant(tenant.Id);

        _db.Users.Add(owner);
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthSession(owner.Id, tenant.Id, tenant.Slug, owner.FullName, owner.Role);
    }

    private static DomainRuleViolationException SlugTaken() => new(
        "tenant.slug_taken",
        "این نشانی قبلاً برای مطب دیگری ثبت شده است.");
}
