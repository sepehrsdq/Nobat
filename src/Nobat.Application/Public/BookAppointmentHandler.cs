using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Appointments;
using Nobat.Domain.Common;
using Nobat.Domain.Scheduling;
using Nobat.Domain.Verification;

namespace Nobat.Application.Public;

/// <summary>
/// Books one slot for a patient. The requested start must be in the free-slot list, which means
/// working hours, the 15-minute grid and the lead time are all checked by the same code the public
/// page reads. The unique index on (TenantId, StaffId, StartUtc) is the last word when two patients
/// press the button at the same moment.
/// </summary>
public sealed class BookAppointmentHandler
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantResolver _tenantResolver;
    private readonly ITenantContextSetter _tenantContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDatabaseErrorTranslator _databaseErrors;
    private readonly TimeProvider _clock;

    public BookAppointmentHandler(
        IApplicationDbContext db,
        ITenantResolver tenantResolver,
        ITenantContextSetter tenantContext,
        IPasswordHasher passwordHasher,
        IDatabaseErrorTranslator databaseErrors,
        TimeProvider clock)
    {
        _db = db;
        _tenantResolver = tenantResolver;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _databaseErrors = databaseErrors;
        _clock = clock;
    }

    public async Task<BookedAppointmentDto> HandleAsync(
        string slug,
        BookAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _tenantResolver.FindBySlugAsync(slug, cancellationToken)
            ?? throw new DomainRuleViolationException("tenant.not_found", "چنین مطبی وجود ندارد.");

        _tenantContext.SetTenant(tenant.Id);

        var service = await _db.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.ServiceId && candidate.IsActive,
                cancellationToken)
            ?? throw new DomainRuleViolationException("service.not_found", "این خدمت در دسترس نیست.");

        var staff = await _db.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.StaffId && candidate.IsActive,
                cancellationToken)
            ?? throw new DomainRuleViolationException("staff.not_found", "این پرسنل در دسترس نیست.");

        var phone = IranianPhone.Normalize(request.Phone);
        var now = _clock.GetUtcNow();
        var start = request.StartUtc.ToUniversalTime();
        var end = start.AddMinutes(service.DurationMinutes);

        var freeSlots = await SlotLookup.FreeSlotsAsync(
            _db,
            staff.Id,
            service.DurationMinutes,
            SlotLookup.LocalDateOf(start),
            now,
            cancellationToken);

        if (!freeSlots.Any(slot => slot.StartUtc == start))
        {
            throw new DomainRuleViolationException(
                "appointment.slot_unavailable",
                "این زمان آزاد نیست. زمان دیگری انتخاب کنید.");
        }

        // The code is checked only after the slot is known to be free, so a mistyped slot
        // does not burn the patient's one-time code.
        await ConfirmCodeAsync(phone, request.Code, now, cancellationToken);

        var appointment = Appointment.Create(
            tenant.Id,
            staff.Id,
            service.Id,
            request.PatientName,
            phone,
            start,
            end,
            note: null,
            now);

        _db.Appointments.Add(appointment);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (_databaseErrors.IsUniqueViolation(exception))
        {
            // Two requests reached the database with the same start; the filtered unique index
            // decided. The caller re-reads the slot list and picks another time.
            throw new DomainRuleViolationException(
                "appointment.slot_taken",
                "همین لحظه این زمان پر شد. زمان دیگری انتخاب کنید.");
        }

        return new BookedAppointmentDto(
            appointment.Id,
            appointment.StartUtc,
            appointment.EndUtc,
            staff.DisplayName,
            service.Name,
            service.DurationMinutes,
            service.PriceRial);
    }

    private async Task ConfirmCodeAsync(
        string phone,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var challenge = await _db.OtpChallenges
            .Where(candidate => candidate.PhoneNumber == phone)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainRuleViolationException(
                "otp.required",
                "برای این شماره کدی ثبت نشده است. اول کد تأیید بگیرید.");

        var normalized = OtpCode.Normalize(code);
        var matches = OtpCode.IsWellFormed(normalized)
            && _passwordHasher.Verify(challenge.CodeHash, normalized);

        try
        {
            challenge.Confirm(matches, now);
        }
        finally
        {
            // A wrong guess still increments the attempt counter; without this save the limit
            // would reset on every request and ten thousand guesses would cost nothing.
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
