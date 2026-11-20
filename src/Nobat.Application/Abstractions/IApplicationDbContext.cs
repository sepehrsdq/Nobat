using Microsoft.EntityFrameworkCore;
using Nobat.Domain.Appointments;
using Nobat.Domain.Catalog;
using Nobat.Domain.Personnel;
using Nobat.Domain.Scheduling;
using Nobat.Domain.Tenants;
using Nobat.Domain.Users;
using Nobat.Domain.Verification;

namespace Nobat.Application.Abstractions;

/// <summary>
/// The slice of EF Core the use-cases need. Implemented by <c>NobatDbContext</c> in
/// Infrastructure; every set except <see cref="Tenants"/> is tenant-filtered.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<User> Users { get; }

    DbSet<Staff> Staff { get; }

    DbSet<Service> Services { get; }

    DbSet<WorkingHour> WorkingHours { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<OtpChallenge> OtpChallenges { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
