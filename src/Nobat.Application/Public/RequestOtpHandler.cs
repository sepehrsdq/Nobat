using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nobat.Application.Abstractions;
using Nobat.Application.Notifications;
using Nobat.Domain.Common;
using Nobat.Domain.Verification;

namespace Nobat.Application.Public;

/// <summary>
/// Issues a login-free code for a patient's phone. The plain code leaves the building exactly
/// once, through <see cref="ISmsSender"/>; only its hash is stored.
/// </summary>
public sealed class RequestOtpHandler
{
    /// <summary>Three codes per number in ten minutes: enough for a real retry, useless for abuse.</summary>
    public const int MaxRequestsPerWindow = 3;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextSetter _tenantContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISmsSender _smsSender;
    private readonly IOptions<SmsOptions> _smsOptions;
    private readonly TimeProvider _clock;

    public RequestOtpHandler(
        IApplicationDbContext db,
        ITenantResolver tenantResolver,
        ITenantContextSetter tenantContext,
        IPasswordHasher passwordHasher,
        ISmsSender smsSender,
        IOptions<SmsOptions> smsOptions,
        TimeProvider clock)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _smsSender = smsSender;
        _smsOptions = smsOptions;
        _clock = clock;
    }

    public async Task<OtpRequestResult> HandleAsync(
        string slug,
        RequestOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _tenantResolver.FindBySlugAsync(slug, cancellationToken)
            ?? throw new DomainRuleViolationException("tenant.not_found", "چنین مطبی وجود ندارد.");

        _tenantContext.SetTenant(tenant.Id);

        var phone = IranianPhone.Normalize(request.Phone);
        var now = _clock.GetUtcNow();

        var recentRequests = await _db.OtpChallenges
            .CountAsync(
                challenge => challenge.PhoneNumber == phone && challenge.CreatedAtUtc >= now - Window,
                cancellationToken);

        if (recentRequests >= MaxRequestsPerWindow)
        {
            throw new DomainRuleViolationException(
                "otp.throttled",
                "برای این شماره چند کد فرستاده شده است. چند دقیقه بعد دوباره تلاش کنید.");
        }

        var code = OtpCode.Generate();
        var challenge = OtpChallenge.Issue(tenant.Id, phone, _passwordHasher.Hash(code), now);

        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        await _smsSender.SendVerificationCodeAsync(phone, code, cancellationToken);

        return new OtpRequestResult(
            (int)OtpChallenge.DefaultLifetime.TotalSeconds,
            _smsOptions.Value.ExposeCodeInResponse ? code : null);
    }
}
