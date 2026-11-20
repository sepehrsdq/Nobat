using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class StaffAndServicesTests
{
    private readonly SqlServerDatabaseFixture _database;

    public StaffAndServicesTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Staff_CreateThenList_ReturnsTheNewMember()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);

        var created = await client.PostAsJsonAsync("/api/staff", new
        {
            displayName = "دکتر رضایی",
            title = "اطفال",
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var staff = await created.Content.ReadFromJsonAsync<StaffResponse>();
        staff.ShouldNotBeNull();
        staff.IsActive.ShouldBeTrue();

        var list = await client.GetFromJsonAsync<List<StaffResponse>>("/api/staff");
        list.ShouldNotBeNull();
        list.ShouldContain(member => member.Id == staff.Id && member.DisplayName == "دکتر رضایی");
    }

    [Fact]
    public async Task Staff_OfAnotherClinic_IsInvisibleAndNotUpdatable()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var clinicA = factory.CreateClient();
        using var clinicB = factory.CreateClient();

        await ApiHelpers.RegisterClinicAsync(clinicA, "مطب الف");
        await ApiHelpers.RegisterClinicAsync(clinicB, "مطب ب");

        var created = await clinicA.PostAsJsonAsync("/api/staff", new
        {
            displayName = "دکتر الف",
            title = "عمومی",
        });

        var staffOfA = await created.Content.ReadFromJsonAsync<StaffResponse>();
        staffOfA.ShouldNotBeNull();

        var listOfB = await clinicB.GetFromJsonAsync<List<StaffResponse>>("/api/staff");
        listOfB.ShouldNotBeNull();
        listOfB.ShouldBeEmpty();

        var updateByB = await clinicB.PutAsJsonAsync($"/api/staff/{staffOfA.Id}", new
        {
            displayName = "تغییر از مطب دیگر",
            title = (string?)null,
            isActive = false,
        });

        updateByB.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await updateByB.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("staff.not_found");
    }

    [Fact]
    public async Task Staff_Update_ChangesNameTitleAndActivity()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);

        var created = await client.PostAsJsonAsync("/api/staff", new
        {
            displayName = "دکتر رضایی",
            title = (string?)null,
        });

        var staff = await created.Content.ReadFromJsonAsync<StaffResponse>();

        var updated = await client.PutAsJsonAsync($"/api/staff/{staff!.Id}", new
        {
            displayName = "دکتر رضایی‌فر",
            title = "قلب",
            isActive = false,
        });

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await updated.Content.ReadFromJsonAsync<StaffResponse>();
        payload!.DisplayName.ShouldBe("دکتر رضایی‌فر");
        payload.Title.ShouldBe("قلب");
        payload.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Service_InvalidDuration_ReturnsBadRequestWithStableCode()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);

        var response = await client.PostAsJsonAsync("/api/services", new
        {
            name = "ویزیت کوتاه",
            durationMinutes = 7,
            priceRial = 500_000,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("service.duration_invalid");
    }

    [Fact]
    public async Task Service_CreateThenUpdate_ChangesDurationAndPrice()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        await ApiHelpers.RegisterClinicAsync(client);

        var created = await client.PostAsJsonAsync("/api/services", new
        {
            name = "ویزیت",
            durationMinutes = 30,
            priceRial = 500_000,
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var service = await created.Content.ReadFromJsonAsync<ServiceResponse>();
        service!.IsActive.ShouldBeTrue();

        var updated = await client.PutAsJsonAsync($"/api/services/{service.Id}", new
        {
            name = "ویزیت تخصصی",
            durationMinutes = 45,
            priceRial = 900_000,
            isActive = true,
        });

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await updated.Content.ReadFromJsonAsync<ServiceResponse>();
        payload!.Name.ShouldBe("ویزیت تخصصی");
        payload.DurationMinutes.ShouldBe(45);
        payload.PriceRial.ShouldBe(900_000);

        var list = await client.GetFromJsonAsync<List<ServiceResponse>>("/api/services");
        list.ShouldNotBeNull();
        list.ShouldContain(item => item.Id == service.Id && item.DurationMinutes == 45);
    }

    [Fact]
    public async Task StaffAndServices_WithoutCookie_ReturnUnauthorized()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/staff")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/services")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record StaffResponse(Guid Id, string DisplayName, string? Title, bool IsActive);

    private sealed record ServiceResponse(Guid Id, string Name, int DurationMinutes, long PriceRial, bool IsActive);
}
