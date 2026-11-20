using Microsoft.EntityFrameworkCore;
using Nobat.Domain.Appointments;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class SlotUniquenessTests
{
    private static readonly DateTimeOffset SlotStart = new(2026, 12, 5, 6, 0, 0, TimeSpan.Zero);

    private readonly SqlServerDatabaseFixture _database;

    public SlotUniquenessTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task SecondBookingOfTheSameSlot_IsRejectedByTheIndex()
    {
        var clinic = await TestDb.SeedClinicAsync(_database.ConnectionString, "slot-one");

        await using (var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(clinic));
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(clinic));

            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task CancelledAppointment_FreesTheSlot()
    {
        var clinic = await TestDb.SeedClinicAsync(_database.ConnectionString, "slot-cancel");

        await using (var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id))
        {
            var first = NewAppointment(clinic);
            db.Appointments.Add(first);
            await db.SaveChangesAsync();

            first.Cancel(TestDb.Now, "بیمار منصرف شد");
            await db.SaveChangesAsync();
        }

        await using (var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id))
        {
            db.Appointments.Add(NewAppointment(clinic));
            await db.SaveChangesAsync();

            (await db.Appointments.CountAsync()).ShouldBe(2);
        }
    }

    [Fact]
    public async Task TwoParallelBookings_ExactlyOneWins()
    {
        var clinic = await TestDb.SeedClinicAsync(_database.ConnectionString, "slot-race");

        var attempts = await Task.WhenAll(
            TryBookAsync(clinic),
            TryBookAsync(clinic));

        attempts.Count(success => success).ShouldBe(1);

        await using var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id);
        (await db.Appointments.CountAsync()).ShouldBe(1);
    }

    private async Task<bool> TryBookAsync(Clinic clinic)
    {
        await using var db = TestDb.Create(_database.ConnectionString, clinic.Tenant.Id);
        db.Appointments.Add(NewAppointment(clinic));

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private static Appointment NewAppointment(Clinic clinic) => Appointment.Create(
        clinic.Tenant.Id,
        clinic.Staff.Id,
        clinic.Service.Id,
        "بیمار تست",
        "09121234567",
        SlotStart,
        SlotStart.AddMinutes(30),
        null,
        TestDb.Now);
}
