using Microsoft.EntityFrameworkCore;
using Nobat.Domain.Appointments;
using Nobat.Infrastructure.Persistence;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class TenantIsolationTests
{
    private readonly SqlServerDatabaseFixture _database;

    public TenantIsolationTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Appointments_OfOneTenant_AreInvisibleToAnother()
    {
        var alpha = await TestDb.SeedClinicAsync(_database.ConnectionString, "alpha");
        var beta = await TestDb.SeedClinicAsync(_database.ConnectionString, "beta");

        await using (var db = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(alpha));
            await db.SaveChangesAsync();
        }

        await using var alphaDb = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id);
        (await alphaDb.Appointments.CountAsync()).ShouldBe(1);

        await using var betaDb = TestDb.Create(_database.ConnectionString, beta.Tenant.Id);
        (await betaDb.Appointments.CountAsync()).ShouldBe(0);
        (await betaDb.Staff.CountAsync()).ShouldBe(1);
        (await betaDb.Services.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Appointments_WithUnresolvedTenant_ReturnNothing()
    {
        var clinic = await TestDb.SeedClinicAsync(_database.ConnectionString, "gamma");

        await using (var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(clinic));
            await db.SaveChangesAsync();
        }

        await using var unresolvedDb = TestDb.Create(_database.ConnectionString, tenantId: null);

        (await unresolvedDb.Appointments.CountAsync()).ShouldBe(0);
        (await unresolvedDb.Staff.CountAsync()).ShouldBe(0);
        (await unresolvedDb.Services.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SaveChanges_WithoutResolvedTenant_ThrowsAndWritesNothing()
    {
        var clinic = await TestDb.SeedClinicAsync(_database.ConnectionString, "delta");

        await using var db = TestDb.Create(_database.ConnectionString, tenantId: null);
        db.Appointments.Add(NewAppointment(clinic));

        await Should.ThrowAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChanges_ForAnotherTenant_Throws()
    {
        var alpha = await TestDb.SeedClinicAsync(_database.ConnectionString, "epsilon");
        var beta = await TestDb.SeedClinicAsync(_database.ConnectionString, "zeta");

        await using var db = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id);

        var foreignAppointment = Appointment.Create(
            beta.Tenant.Id,
            beta.Staff.Id,
            beta.Service.Id,
            "بیمار",
            "09121234567",
            new DateTimeOffset(2026, 12, 1, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 1, 6, 30, 0, TimeSpan.Zero),
            null,
            TestDb.Now);

        db.Appointments.Add(foreignAppointment);

        await Should.ThrowAsync<TenantIsolationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task ExecuteDelete_RespectsTheGlobalFilter()
    {
        var alpha = await TestDb.SeedClinicAsync(_database.ConnectionString, "eta");
        var beta = await TestDb.SeedClinicAsync(_database.ConnectionString, "theta");

        await using (var db = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(alpha));
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.Create(_database.ConnectionString, beta.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(beta));
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id))
        {
            var deleted = await db.Appointments.ExecuteDeleteAsync();
            deleted.ShouldBe(1);
        }

        await using var betaDb = TestDb.Create(_database.ConnectionString, beta.Tenant.Id);
        (await betaDb.Appointments.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AppointmentPointingAtAnotherTenantsStaff_IsRejectedByTheDatabase()
    {
        var alpha = await TestDb.SeedClinicAsync(_database.ConnectionString, "iota");
        var beta = await TestDb.SeedClinicAsync(_database.ConnectionString, "kappa");

        await using var db = TestDb.Create(_database.ConnectionString, alpha.Tenant.Id);

        var appointment = Appointment.Create(
            alpha.Tenant.Id,
            beta.Staff.Id,
            alpha.Service.Id,
            "بیمار",
            "09121234567",
            new DateTimeOffset(2026, 12, 2, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 2, 6, 30, 0, TimeSpan.Zero),
            null,
            TestDb.Now);

        db.Appointments.Add(appointment);

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static Appointment NewAppointment(Clinic clinic)
    {
        var start = new DateTimeOffset(2026, 12, 1, 6, 0, 0, TimeSpan.Zero);

        return Appointment.Create(
            clinic.Tenant.Id,
            clinic.Staff.Id,
            clinic.Service.Id,
            "بیمار تست",
            "09121234567",
            start,
            start.AddMinutes(30),
            null,
            TestDb.Now);
    }
}
