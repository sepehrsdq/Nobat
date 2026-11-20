using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nobat.Domain.Appointments;
using Nobat.Domain.Scheduling;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PanelAppointmentsTests
{
    private readonly SqlServerDatabaseFixture _database;

    public PanelAppointmentsTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task List_ReturnsTheDaysAppointmentsWithNamesAndPhone()
    {
        using var clinic = await ClinicAsync();
        await SeedAsync(clinic, 9, 0, "مریم احمدی");
        await SeedAsync(clinic, 10, 0, "حسن کریمی");

        var day = await GetDayAsync(clinic.Client, clinic.Date);

        day.Date.ShouldBe(clinic.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        day.Items.Count.ShouldBe(2);
        day.Items[0].PatientName.ShouldBe("مریم احمدی");
        day.Items[0].StaffName.ShouldBe("دکتر رضایی");
        day.Items[0].ServiceName.ShouldBe("ویزیت");
        day.Items[0].PatientPhone.ShouldBe("09121234567");
        day.Items[0].Status.ShouldBe("Booked");
        day.Items[1].StartUtc.ShouldBeGreaterThan(day.Items[0].StartUtc);
    }

    [Fact]
    public async Task List_WithoutADate_MeansTodayInTehran()
    {
        using var clinic = await ClinicAsync();
        var today = TodayInTehran();
        await SeedAsync(clinic, localDate: today, hour: 23, minute: 0, patientName: "بیمار امشب");

        var day = await GetDayAsync(clinic.Client, date: null);

        day.Date.ShouldBe(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        day.Items.ShouldContain(item => item.PatientName == "بیمار امشب");
    }

    [Fact]
    public async Task List_ShowsNothingFromAnotherClinic()
    {
        using var clinicA = await ClinicAsync();
        using var clinicB = await ClinicAsync();
        await SeedAsync(clinicA, 9, 0, "بیمار مطب اول");

        var day = await GetDayAsync(clinicB.Client, clinicA.Date);

        day.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_MarksTheRowAndFreesTheSlot()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        var response = await clinic.Client.PostAsJsonAsync(
            $"/api/appointments/{appointment}/cancel",
            new { reason = "بیمار تماس گرفت" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelled = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        cancelled.ShouldNotBeNull();
        cancelled.Status.ShouldBe("Cancelled");
        cancelled.CancellationReason.ShouldBe("بیمار تماس گرفت");

        var slots = await GetSlotsAsync(clinic);
        slots.ShouldContain(slot => slot.StartUtc == AppointmentStart(clinic.Date, 9, 0));
    }

    [Fact]
    public async Task Cancel_Twice_ReturnsConflict()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        (await clinic.Client.PostAsJsonAsync($"/api/appointments/{appointment}/cancel", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await clinic.Client.PostAsJsonAsync($"/api/appointments/{appointment}/cancel", new { });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.not_cancellable");
    }

    [Fact]
    public async Task Cancel_FromAnotherClinic_ReturnsNotFound()
    {
        using var clinicA = await ClinicAsync();
        using var clinicB = await ClinicAsync();
        var appointment = await SeedAsync(clinicA, 9, 0, "مریم احمدی");

        var response = await clinicB.Client.PostAsJsonAsync($"/api/appointments/{appointment}/cancel", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.not_found");
    }

    [Fact]
    public async Task Reschedule_MovesTheAppointmentAndFreesTheOldSlot()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");
        var newStart = AppointmentStart(clinic.Date, 10, 0);

        var response = await clinic.Client.PostAsJsonAsync(
            $"/api/appointments/{appointment}/reschedule",
            new { startUtc = newStart });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var moved = await response.Content.ReadFromJsonAsync<AppointmentResponse>();
        moved.ShouldNotBeNull();
        moved.Id.ShouldBe(appointment);
        moved.StartUtc.ShouldBe(newStart);
        moved.EndUtc.ShouldBe(newStart.AddMinutes(30));

        var slots = await GetSlotsAsync(clinic);
        slots.ShouldContain(slot => slot.StartUtc == AppointmentStart(clinic.Date, 9, 0));
        slots.ShouldNotContain(slot => slot.StartUtc == newStart);

        var day = await GetDayAsync(clinic.Client, clinic.Date);
        day.Items.Count.ShouldBe(1);
        day.Items[0].StartUtc.ShouldBe(newStart);
    }

    [Fact]
    public async Task Reschedule_OntoATakenSlot_ReturnsBadRequestAndKeepsTheOldTime()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");
        await SeedAsync(clinic, 10, 0, "حسن کریمی");

        var response = await clinic.Client.PostAsJsonAsync(
            $"/api/appointments/{appointment}/reschedule",
            new { startUtc = AppointmentStart(clinic.Date, 10, 0) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.slot_unavailable");

        var day = await GetDayAsync(clinic.Client, clinic.Date);
        day.Items.Single(item => item.Id == appointment).StartUtc
            .ShouldBe(AppointmentStart(clinic.Date, 9, 0));
    }

    [Fact]
    public async Task Reschedule_OutsideWorkingHours_ReturnsBadRequest()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        var response = await clinic.Client.PostAsJsonAsync(
            $"/api/appointments/{appointment}/reschedule",
            new { startUtc = AppointmentStart(clinic.Date, 15, 0) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.slot_unavailable");
    }

    [Fact]
    public async Task Reschedule_FromAnotherClinic_ReturnsNotFound()
    {
        using var clinicA = await ClinicAsync();
        using var clinicB = await ClinicAsync();
        var appointment = await SeedAsync(clinicA, 9, 0, "مریم احمدی");

        var response = await clinicB.Client.PostAsJsonAsync(
            $"/api/appointments/{appointment}/reschedule",
            new { startUtc = AppointmentStart(clinicB.Date, 10, 0) });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.not_found");
    }

    [Fact]
    public async Task Endpoints_WithoutASession_ReturnUnauthorized()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        using var anonymous = new NobatApiFactory(_database.ConnectionString).CreateClient();

        (await anonymous.GetAsync($"/api/appointments?date={clinic.Date:yyyy-MM-dd}"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await anonymous.PostAsJsonAsync($"/api/appointments/{appointment}/cancel", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RescheduleSlots_ExcludeTheAppointmentsOwnStart()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        var slots = await clinic.Client.GetFromJsonAsync<RescheduleSlotsResponse>(
            $"/api/appointments/{appointment}/slots?date={clinic.Date:yyyy-MM-dd}");

        slots.ShouldNotBeNull();
        slots.Date.ShouldBe(clinic.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        slots.Slots.ShouldNotContain(slot => slot.StartUtc == AppointmentStart(clinic.Date, 9, 0));
        slots.Slots.ShouldContain(slot => slot.StartUtc == AppointmentStart(clinic.Date, 10, 0));
    }

    [Fact]
    public async Task RescheduleSlots_WithoutADate_UseTheAppointmentsOwnDay()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");

        var slots = await clinic.Client.GetFromJsonAsync<RescheduleSlotsResponse>(
            $"/api/appointments/{appointment}/slots");

        slots.ShouldNotBeNull();
        slots.Date.ShouldBe(clinic.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task RescheduleSlots_ForACancelledAppointment_ReturnConflict()
    {
        using var clinic = await ClinicAsync();
        var appointment = await SeedAsync(clinic, 9, 0, "مریم احمدی");
        (await clinic.Client.PostAsJsonAsync($"/api/appointments/{appointment}/cancel", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await clinic.Client.GetAsync($"/api/appointments/{appointment}/slots");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("appointment.not_reschedulable");
    }

    private async Task<Clinic> ClinicAsync()
    {
        var factory = new NobatApiFactory(_database.ConnectionString);
        var client = factory.CreateClient();
        var slug = await ApiHelpers.RegisterClinicAsync(client);

        var staffResponse = await client.PostAsJsonAsync("/api/staff", new
        {
            displayName = "دکتر رضایی",
            title = "اطفال",
        });
        staffResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var serviceResponse = await client.PostAsJsonAsync("/api/services", new
        {
            name = "ویزیت",
            durationMinutes = 30,
            priceRial = 500_000,
        });
        serviceResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var staffId = (await staffResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<IdResponse>())!.Id;

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        await SetWorkingHoursAsync(client, staffId, date.DayOfWeek);

        using var probe = TestDb.Create(_database.ConnectionString, tenantId: null);
        var tenantId = probe.Tenants.First(tenant => tenant.Slug == slug).Id;

        return new Clinic(factory, client, tenantId, staffId, serviceId, date, slug);
    }

    private async Task<Guid> SeedAsync(Clinic clinic, int hour, int minute, string patientName)
        => await SeedAsync(clinic, clinic.Date, hour, minute, patientName);

    private async Task<Guid> SeedAsync(
        Clinic clinic,
        DateOnly localDate,
        int hour,
        int minute,
        string patientName)
    {
        var start = AppointmentStart(localDate, hour, minute);

        var appointment = Appointment.Create(
            clinic.TenantId,
            clinic.StaffId,
            clinic.ServiceId,
            patientName,
            "09121234567",
            start,
            start.AddMinutes(30),
            note: null,
            DateTimeOffset.UtcNow);

        using var db = TestDb.Create(_database.ConnectionString, clinic.TenantId);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return appointment.Id;
    }

    private static async Task SetWorkingHoursAsync(HttpClient client, Guid staffId, DayOfWeek day)
    {
        var response = await client.PutAsJsonAsync($"/api/staff/{staffId}/working-hours", new
        {
            ranges = new object[]
            {
                new
                {
                    dayOfWeek = day.ToString(),
                    startTime = "09:00:00",
                    endTime = "12:00:00",
                },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<DayResponse> GetDayAsync(HttpClient client, DateOnly? date)
    {
        var url = date is null
            ? "/api/appointments"
            : $"/api/appointments?date={date:yyyy-MM-dd}";

        var day = await client.GetFromJsonAsync<DayResponse>(url);

        day.ShouldNotBeNull();
        return day;
    }

    private static async Task<List<SlotResponse>> GetSlotsAsync(Clinic clinic)
    {
        var url = $"/api/public/{clinic.Slug}/slots?serviceId={clinic.ServiceId}&staffId={clinic.StaffId}&date={clinic.Date:yyyy-MM-dd}";
        var slots = await clinic.Client.GetFromJsonAsync<List<SlotResponse>>(url);

        slots.ShouldNotBeNull();
        return slots;
    }

    private static DateOnly TodayInTehran()
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ClinicTime.Zone).DateTime);

    private static DateTimeOffset AppointmentStart(DateOnly date, int hour, int minute)
        => new(
            TimeZoneInfo.ConvertTimeToUtc(
                date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified),
                ClinicTime.Zone),
            TimeSpan.Zero);

    private sealed record Clinic(
        NobatApiFactory Factory,
        HttpClient Client,
        Guid TenantId,
        Guid StaffId,
        Guid ServiceId,
        DateOnly Date,
        string Slug = "") : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    private sealed record IdResponse(Guid Id);

    private sealed record SlotResponse(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    private sealed record DayResponse(string Date, List<AppointmentResponse> Items);

    private sealed record RescheduleSlotsResponse(string Date, List<SlotResponse> Slots);

    private sealed record AppointmentResponse(
        Guid Id,
        string StaffName,
        string ServiceName,
        string PatientName,
        string PatientPhone,
        DateTimeOffset StartUtc,
        DateTimeOffset EndUtc,
        string Status,
        string? CancellationReason);
}
