using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Appointments;

namespace Nobat.Application.Appointments;

/// <summary>
/// Turns appointment rows into the panel shape. The two name lookups are kept in one place so the
/// list, the cancel response and the reschedule response cannot drift apart.
/// </summary>
internal static class AppointmentProjection
{
    private const string UnknownName = "—";

    public static async Task<IReadOnlyList<AppointmentDto>> MapAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        if (appointments.Count == 0)
        {
            return Array.Empty<AppointmentDto>();
        }

        var staffIds = appointments.Select(appointment => appointment.StaffId).Distinct().ToArray();
        var serviceIds = appointments.Select(appointment => appointment.ServiceId).Distinct().ToArray();

        var staffNames = await db.Staff
            .AsNoTracking()
            .Where(staff => staffIds.Contains(staff.Id))
            .ToDictionaryAsync(staff => staff.Id, staff => staff.DisplayName, cancellationToken);

        var services = await db.Services
            .AsNoTracking()
            .Where(service => serviceIds.Contains(service.Id))
            .ToDictionaryAsync(
                service => service.Id,
                service => new { service.Name, service.PriceRial },
                cancellationToken);

        return appointments
            .Select(appointment =>
            {
                var service = services.GetValueOrDefault(appointment.ServiceId);

                return new AppointmentDto(
                    appointment.Id,
                    appointment.StaffId,
                    staffNames.GetValueOrDefault(appointment.StaffId, UnknownName),
                    appointment.ServiceId,
                    service?.Name ?? UnknownName,
                    appointment.PatientName,
                    appointment.PatientPhone,
                    appointment.StartUtc,
                    appointment.EndUtc,
                    (int)(appointment.EndUtc - appointment.StartUtc).TotalMinutes,
                    service?.PriceRial ?? 0,
                    appointment.Status.ToString(),
                    appointment.Note,
                    appointment.CancelledAtUtc,
                    appointment.CancellationReason);
            })
            .ToArray();
    }

    public static async Task<AppointmentDto> MapSingleAsync(
        IApplicationDbContext db,
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var mapped = await MapAsync(db, new[] { appointment }, cancellationToken);

        return mapped[0];
    }
}
