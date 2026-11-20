using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PublicBookingTests
{
    private const string PatientPhone = "09121234567";

    private readonly SqlServerDatabaseFixture _database;

    public PublicBookingTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Booking_WithAValidCode_ReservesTheSlot()
    {
        using var clinic = await BookableClinicAsync();
        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);
        var slot = slots[0];

        var code = await RequestCodeAsync(clinic.Client, clinic.Slug);
        var response = await BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slot.StartUtc, code);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var booked = await response.Content.ReadFromJsonAsync<BookedResponse>();
        booked.ShouldNotBeNull();
        booked.StartUtc.ShouldBe(slot.StartUtc);
        booked.StaffName.ShouldBe("دکتر رضایی");
        booked.DurationMinutes.ShouldBe(30);

        var afterBooking = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);
        afterBooking.ShouldNotContain(candidate => candidate.StartUtc == slot.StartUtc);
    }

    [Fact]
    public async Task Booking_WithAWrongCode_KeepsTheCodeUsable()
    {
        using var clinic = await BookableClinicAsync();
        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);

        var code = await RequestCodeAsync(clinic.Client, clinic.Slug);
        var wrongCode = code == "000000" ? "111111" : "000000";

        var rejected = await BookAsync(
            clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, wrongCode);

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("otp.invalid");

        var accepted = await BookAsync(
            clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Booking_WithoutAnyCode_ReturnsBadRequest()
    {
        using var clinic = await BookableClinicAsync();
        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);

        var response = await BookAsync(
            clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, "123456");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("otp.required");
    }

    [Fact]
    public async Task Booking_OutsideWorkingHours_IsRejectedAndKeepsTheCode()
    {
        using var clinic = await BookableClinicAsync();
        var code = await RequestCodeAsync(clinic.Client, clinic.Slug);

        // 07:00 Tehran, two hours before the clinic opens.
        var early = new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(
                clinic.Date.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Unspecified),
                Nobat.Domain.Scheduling.ClinicTime.Zone),
            TimeSpan.Zero);

        var rejected = await BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, early, code);

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.slot_unavailable");

        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);
        var accepted = await BookAsync(
            clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Booking_AnAlreadyTakenSlot_ReturnsBadRequestWithASlotCode()
    {
        using var clinic = await BookableClinicAsync();
        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);

        var first = await RequestCodeAsync(clinic.Client, clinic.Slug);
        (await BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, first))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await RequestCodeAsync(clinic.Client, clinic.Slug);
        var again = await BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, second);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.slot_unavailable");
    }

    [Fact]
    public async Task Bookings_RacingForTheSameSlot_StoreExactlyOne()
    {
        using var clinic = await BookableClinicAsync();
        var slots = await GetSlotsAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, clinic.Date);

        // Two patients, two codes, one slot, no coordination between them.
        var codeOne = await RequestCodeAsync(clinic.Client, clinic.Slug, "09121234567");
        var codeTwo = await RequestCodeAsync(clinic.Client, clinic.Slug, "09351234567");

        var first = BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, codeOne, "09121234567");
        var second = BookAsync(clinic.Client, clinic.Slug, clinic.ServiceId, clinic.StaffId, slots[0].StartUtc, codeTwo, "09351234567");

        var responses = await Task.WhenAll(first, second);

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);

        foreach (var loser in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            loser.StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
        }

        using var db = TestDb.Create(_database.ConnectionString, clinic.TenantId);
        db.Appointments.Count(appointment => appointment.StartUtc == slots[0].StartUtc).ShouldBe(1);
    }

    [Fact]
    public async Task RequestingTooManyCodes_IsThrottled()
    {
        using var clinic = await BookableClinicAsync();

        // Three codes per number in ten minutes, mirroring RequestOtpHandler.MaxRequestsPerWindow.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await RequestCodeAsync(clinic.Client, clinic.Slug);
        }

        var throttled = await clinic.Client.PostAsJsonAsync($"/api/public/{clinic.Slug}/otp", new { phone = PatientPhone });

        throttled.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await throttled.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("otp.throttled");
    }

    [Fact]
    public async Task Booking_WithAnotherClinicsService_ReturnsNotFound()
    {
        using var clinicA = await BookableClinicAsync();
        using var clinicB = await BookableClinicAsync();

        var slots = await GetSlotsAsync(clinicA.Client, clinicA.Slug, clinicA.ServiceId, clinicA.StaffId, clinicA.Date);
        var code = await RequestCodeAsync(clinicA.Client, clinicA.Slug);

        var response = await BookAsync(
            clinicA.Client, clinicA.Slug, clinicB.ServiceId, clinicA.StaffId, slots[0].StartUtc, code);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("service.not_found");
    }

    [Fact]
    public async Task Booking_BelongsToTheClinicThatTookIt()
    {
        using var clinicA = await BookableClinicAsync();
        using var clinicB = await BookableClinicAsync();

        var slots = await GetSlotsAsync(clinicA.Client, clinicA.Slug, clinicA.ServiceId, clinicA.StaffId, clinicA.Date);
        var code = await RequestCodeAsync(clinicA.Client, clinicA.Slug);

        (await BookAsync(clinicA.Client, clinicA.Slug, clinicA.ServiceId, clinicA.StaffId, slots[0].StartUtc, code))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        using var dbOfA = TestDb.Create(_database.ConnectionString, clinicA.TenantId);
        using var dbOfB = TestDb.Create(_database.ConnectionString, clinicB.TenantId);

        dbOfA.Appointments.Count().ShouldBe(1);
        dbOfB.Appointments.Count().ShouldBe(0);
    }

    private async Task<BookableClinic> BookableClinicAsync()
    {
        var factory = new NobatApiFactory(_database.ConnectionString);
        var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);
        var staffId = await CreateStaffAsync(client);
        var serviceId = await CreateServiceAsync(client);

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        await SetWorkingHoursAsync(client, staffId, date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(12, 0));

        using var probe = TestDb.Create(_database.ConnectionString, tenantId: null);
        var tenantId = probe.Tenants.First(tenant => tenant.Slug == slug).Id;

        return new BookableClinic(factory, client, tenantId, slug, staffId, serviceId, date);
    }

    private static async Task<Guid> CreateStaffAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/staff", new
        {
            displayName = "دکتر رضایی",
            title = "اطفال",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var staff = await response.Content.ReadFromJsonAsync<IdResponse>();
        return staff!.Id;
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/services", new
        {
            name = "ویزیت",
            durationMinutes = 30,
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
        var slots = await client.GetFromJsonAsync<List<SlotResponse>>(url);

        slots.ShouldNotBeNull();
        slots.ShouldNotBeEmpty();

        return slots;
    }

    private static async Task<string> RequestCodeAsync(
        HttpClient client,
        string slug,
        string phone = PatientPhone)
    {
        var response = await client.PostAsJsonAsync($"/api/public/{slug}/otp", new { phone });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<OtpResponse>();
        payload.ShouldNotBeNull();
        payload.ExpiresInSeconds.ShouldBe(120);
        payload.DevCode.ShouldNotBeNullOrWhiteSpace();

        return payload.DevCode!;
    }

    private static Task<HttpResponseMessage> BookAsync(
        HttpClient client,
        string slug,
        Guid serviceId,
        Guid staffId,
        DateTimeOffset startUtc,
        string code,
        string phone = PatientPhone)
        => client.PostAsJsonAsync($"/api/public/{slug}/appointments", new
        {
            serviceId,
            staffId,
            startUtc,
            patientName = "بیمار تست",
            phone,
            code,
        });

    private sealed record BookableClinic(
        NobatApiFactory Factory,
        HttpClient Client,
        Guid TenantId,
        string Slug,
        Guid StaffId,
        Guid ServiceId,
        DateOnly Date) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private sealed record IdResponse(Guid Id);

    private sealed record OtpResponse(int ExpiresInSeconds, string? DevCode);

    private sealed record SlotResponse(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    private sealed record BookedResponse(
        Guid Id,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        string StaffName,
        string ServiceName,
        int DurationMinutes,
        long PriceRial);
}
