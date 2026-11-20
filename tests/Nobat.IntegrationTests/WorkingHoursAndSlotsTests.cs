using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nobat.Domain.Appointments;
using Nobat.Domain.Scheduling;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class WorkingHoursAndSlotsTests
{
    private readonly SqlServerDatabaseFixture _database;

    public WorkingHoursAndSlotsTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task WorkingHours_ReplaceThenGet_RoundTripsAndReplaces()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);

        var replace = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new { dayOfWeek = "Saturday", startTime = "09:00:00", endTime = "13:00:00" },
                new { dayOfWeek = "Monday", startTime = "10:00:00", endTime = "12:00:00" },
            },
        });

        replace.StatusCode.ShouldBe(HttpStatusCode.OK);

        var first = await client.GetFromJsonAsync<WorkingHoursResponse>($"/api/staff/{staffId}/working-hours");
        first!.Ranges.Count.ShouldBe(2);
        first.Ranges[0].DayOfWeek.ShouldBe("Saturday");
        first.Ranges[0].StartTime.ShouldBe("09:00:00");

        var second = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new { dayOfWeek = "Wednesday", startTime = "08:00:00", endTime = "10:00:00" },
            },
        });

        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterReplace = await client.GetFromJsonAsync<WorkingHoursResponse>($"/api/staff/{staffId}/working-hours");
        afterReplace!.Ranges.Count.ShouldBe(1);
        afterReplace.Ranges[0].DayOfWeek.ShouldBe("Wednesday");
    }

    [Fact]
    public async Task WorkingHours_OverlappingRanges_ReturnBadRequest()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);

        var response = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new { dayOfWeek = "Saturday", startTime = "09:00:00", endTime = "12:00:00" },
                new { dayOfWeek = "Saturday", startTime = "11:00:00", endTime = "14:00:00" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("working_hour.overlap");
    }

    [Fact]
    public async Task WorkingHours_MisspelledDayProperty_IsRejectedInsteadOfDefaulting()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);

        // "day" used to be silently ignored, which stored the range under Sunday (the enum default).
        var response = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new { day = "Saturday", startTime = "09:00:00", endTime = "12:00:00" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var hours = await client.GetFromJsonAsync<WorkingHoursResponse>($"/api/staff/{staffId}/working-hours");
        hours!.Ranges.ShouldBeEmpty();
    }

    [Fact]
    public async Task WorkingHours_OfAnotherTenantsStaff_ReturnNotFound()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var clinicA = factory.CreateClient();
        using var clinicB = factory.CreateClient();

        await ApiHelpers.RegisterClinicAsync(clinicA);
        await ApiHelpers.RegisterClinicAsync(clinicB);
        var staffOfA = await CreateStaffAsync(clinicA);

        var read = await clinicB.GetAsync($"/api/staff/{staffOfA}/working-hours");
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var write = await clinicB.PutAsJsonAsync($"/api/staff/{staffOfA}/working-hours", new
        {
            ranges = new object[]
            {
                new { dayOfWeek = "Saturday", startTime = "09:00:00", endTime = "10:00:00" },
            },
        });

        write.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WorkingHours_ClearingTheWeek_MakesEveryDayUnavailable()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);
        var serviceId = await CreateServiceAsync(client, "ویزیت", durationMinutes: 30);

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        await SetWorkingHoursAsync(client, staffId, date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        (await GetSlotsAsync(client, slug, serviceId, staffId, date)).Count.ShouldBe(11);

        var cleared = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = Array.Empty<object>(),
        });

        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);

        var hours = await client.GetFromJsonAsync<WorkingHoursResponse>($"/api/staff/{staffId}/working-hours");
        hours!.Ranges.ShouldBeEmpty();

        (await GetSlotsAsync(client, slug, serviceId, staffId, date)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PublicClinic_ListsOnlyActiveServicesAndStaff()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);

        var activeStaff = await CreateStaffAsync(client, "دکتر فعال");
        var retiredStaff = await CreateStaffAsync(client, "دکتر بازنشسته");
        var activeService = await CreateServiceAsync(client, "ویزیت فعال");
        var retiredService = await CreateServiceAsync(client, "خدمت حذف‌شده");

        await client.PutAsJsonAsync($"/api/staff/{retiredStaff}", new
        {
            displayName = "دکتر بازنشسته",
            title = (string?)null,
            isActive = false,
        });

        await client.PutAsJsonAsync($"/api/services/{retiredService}", new
        {
            name = "خدمت حذف‌شده",
            durationMinutes = 30,
            priceRial = 100_000,
            isActive = false,
        });

        var publicClinic = await client.GetFromJsonAsync<PublicClinicResponse>($"/api/public/{slug}");

        publicClinic!.Slug.ShouldBe(slug);
        publicClinic.Staff.ShouldContain(member => member.Id == activeStaff);
        publicClinic.Staff.ShouldNotContain(member => member.Id == retiredStaff);
        publicClinic.Services.ShouldContain(service => service.Id == activeService);
        publicClinic.Services.ShouldNotContain(service => service.Id == retiredService);
    }

    [Fact]
    public async Task PublicSlots_ReturnFreeWindowsInsideWorkingHours()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);
        var serviceId = await CreateServiceAsync(client, "ویزیت", durationMinutes: 30);

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        await SetWorkingHoursAsync(client, staffId, date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var slots = await GetSlotsAsync(client, slug, serviceId, staffId, date);

        slots.Count.ShouldBe(11);
        slots[0].StartUtc.ShouldBe(ToTehranUtc(date, new TimeOnly(9, 0)));
        slots[^1].StartUtc.ShouldBe(ToTehranUtc(date, new TimeOnly(11, 30)));
    }

    [Fact]
    public async Task PublicSlots_BookedAppointment_RemovesOverlappingStarts()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);
        var serviceId = await CreateServiceAsync(client, "ویزیت", durationMinutes: 30);

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        await SetWorkingHoursAsync(client, staffId, date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var clinic = FindClinic(slug);

        await using (var db = TestDb.Create(_database.ConnectionString, clinic))
        {
            var start = ToTehranUtc(date, new TimeOnly(9, 30));

            db.Appointments.Add(Appointment.Create(
                clinic,
                staffId,
                serviceId,
                "بیمار تست",
                "09121234567",
                start,
                start.AddMinutes(30),
                null,
                TestDb.Now));

            await db.SaveChangesAsync();
        }

        var slots = await GetSlotsAsync(client, slug, serviceId, staffId, date);

        slots.Count.ShouldBe(8);
        slots.ShouldNotContain(slot => slot.StartUtc == ToTehranUtc(date, new TimeOnly(9, 15)));
        slots.ShouldContain(slot => slot.StartUtc == ToTehranUtc(date, new TimeOnly(9, 0)));
        slots.ShouldContain(slot => slot.StartUtc == ToTehranUtc(date, new TimeOnly(10, 0)));
    }

    [Fact]
    public async Task PublicSlots_UnknownClinic_ReturnNotFound()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/public/no-such-clinic/slots?serviceId={Guid.NewGuid()}&staffId={Guid.NewGuid()}&date=2026-12-05");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("tenant.not_found");
    }

    [Fact]
    public async Task PublicSlots_WithAnotherTenantsService_ReturnNotFound()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var clinicA = factory.CreateClient();
        using var clinicB = factory.CreateClient();

        var slugOfA = await ApiHelpers.RegisterClinicAsync(clinicA);
        await ApiHelpers.RegisterClinicAsync(clinicB);

        var staffOfA = await CreateStaffAsync(clinicA);
        var serviceOfB = await CreateServiceAsync(clinicB, "خدمت مطب ب");

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        var response = await clinicA.GetAsync(
            $"/api/public/{slugOfA}/slots?serviceId={serviceOfB}&staffId={staffOfA}&date={date:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("service.not_found");
    }

    private static async Task<Guid> CreateStaffAsync(HttpClient client, string name = "دکتر رضایی")
    {
        var response = await client.PostAsJsonAsync("/api/staff", new { displayName = name, title = (string?)null });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var staff = await response.Content.ReadFromJsonAsync<IdResponse>();
        return staff!.Id;
    }

    private static async Task<Guid> CreateServiceAsync(
        HttpClient client,
        string name,
        int durationMinutes = 30)
    {
        var response = await client.PostAsJsonAsync("/api/services", new
        {
            name,
            durationMinutes,
            priceRial = 500_000,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var service = await response.Content.ReadFromJsonAsync<IdResponse>();
        return service!.Id;
    }

    private static async Task SetWorkingHoursAsync(
        HttpClient client,
        Guid staffId,
        DayOfWeek day,
        TimeOnly start,
        TimeOnly end)
    {
        var response = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new
                {
                    dayOfWeek = day.ToString(),
                    startTime = start.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    endTime = end.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<List<SlotResponse>> GetSlotsAsync(
        HttpClient client,
        string slug,
        Guid serviceId,
        Guid staffId,
        DateOnly date)
    {
        var url = $"/api/public/{slug}/slots?serviceId={serviceId}&staffId={staffId}&date={date:yyyy-MM-dd}";
        var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var slots = await response.Content.ReadFromJsonAsync<List<SlotResponse>>();
        return slots ?? [];
    }

    private Guid FindClinic(string slug)
    {
        using var db = TestDb.Create(_database.ConnectionString, tenantId: null);
        return db.Tenants.First(candidate => candidate.Slug == slug).Id;
    }

    private static DateTimeOffset ToTehranUtc(DateOnly date, TimeOnly time)
        => new(
            TimeZoneInfo.ConvertTimeToUtc(
                date.ToDateTime(time, DateTimeKind.Unspecified),
                ClinicTime.Zone),
            TimeSpan.Zero);

    private sealed record WorkingHoursResponse(IReadOnlyList<WorkingHourResponse> Ranges);

    private sealed record WorkingHourResponse(string DayOfWeek, string StartTime, string EndTime);

    private sealed record SlotResponse(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    private sealed record IdResponse(Guid Id);

    private sealed record PublicClinicResponse(
        string Slug,
        string Name,
        IReadOnlyList<IdResponse> Services,
        IReadOnlyList<IdResponse> Staff);
}
