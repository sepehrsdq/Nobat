using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Nobat.IntegrationTests.Infrastructure;

namespace Nobat.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AuthTests
{
    private const string OwnerPhone = "09121234567";
    private const string Password = "Nobat1405";

    private readonly SqlServerDatabaseFixture _database;

    public AuthTests(SqlServerDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Register_ThenMe_ReturnsTheNewClinicSession()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = UniqueSlug();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug,
            clinicName = "مطب شمال",
            ownerName = "سپهر",
            phone = OwnerPhone,
            password = Password,
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var me = await client.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await me.Content.ReadFromJsonAsync<MeResponse>();
        payload.ShouldNotBeNull();
        payload.TenantSlug.ShouldBe(slug);
        payload.Role.ShouldBe("Owner");
        payload.FullName.ShouldBe("سپهر");
    }

    [Fact]
    public async Task Register_WithTakenSlug_ReturnsConflict()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = UniqueSlug();

        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug,
            clinicName = "مطب اول",
            ownerName = "سپهر",
            phone = OwnerPhone,
            password = Password,
        });

        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug,
            clinicName = "مطب دوم",
            ownerName = "شخص دیگر",
            phone = "09121112233",
            password = Password,
        });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("tenant.slug_taken");
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsSession()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await RegisterClinicAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            phone = OwnerPhone,
            password = Password,
        });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await login.Content.ReadFromJsonAsync<MeResponse>();
        payload!.TenantSlug.ShouldBe(slug);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorizedWithStableCode()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();
        var slug = await RegisterClinicAsync(client);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            phone = OwnerPhone,
            password = "WrongPass1",
        });

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await login.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Login_ForUnknownClinic_ReturnsTheSameUnauthorizedCode()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = "no-such-clinic",
            phone = OwnerPhone,
            password = Password,
        });

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await login.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Options_ReportWhetherSignupIsOpen()
    {
        using var openFactory = new NobatApiFactory(_database.ConnectionString);
        using var openClient = openFactory.CreateClient();
        var open = await openClient.GetFromJsonAsync<SignupOptions>("/api/auth/options");
        open!.AllowPublicSignup.ShouldBeTrue();

        using var closedFactory = new NobatApiFactory(
            _database.ConnectionString,
            new Dictionary<string, string?> { ["Auth:AllowPublicSignup"] = "false" });
        using var closedClient = closedFactory.CreateClient();
        var closed = await closedClient.GetFromJsonAsync<SignupOptions>("/api/auth/options");
        closed!.AllowPublicSignup.ShouldBeFalse();
    }

    [Fact]
    public async Task Register_WhenSignupIsClosed_ReturnsForbiddenWithStableCode()
    {
        using var factory = new NobatApiFactory(
            _database.ConnectionString,
            new Dictionary<string, string?> { ["Auth:AllowPublicSignup"] = "false" });
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug = UniqueSlug(),
            clinicName = "مطب بسته",
            ownerName = "سپهر",
            phone = OwnerPhone,
            password = Password,
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await register.Content.ReadFromJsonAsync<ProblemDetails>())!.Type.ShouldBe("auth.signup_closed");
    }

    [Fact]
    public async Task Login_StillWorks_WhenSignupIsClosed()
    {
        string slug;

        using (var openFactory = new NobatApiFactory(_database.ConnectionString))
        {
            using var openClient = openFactory.CreateClient();
            slug = await RegisterClinicAsync(openClient);
        }

        using var closedFactory = new NobatApiFactory(
            _database.ConnectionString,
            new Dictionary<string, string?> { ["Auth:AllowPublicSignup"] = "false" });
        using var closedClient = closedFactory.CreateClient();

        var login = await closedClient.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            phone = OwnerPhone,
            password = Password,
        });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await login.Content.ReadFromJsonAsync<MeResponse>())!.TenantSlug.ShouldBe(slug);
    }

    [Fact]
    public async Task Me_WithoutCookie_ReturnsUnauthorized()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var client = factory.CreateClient();

        var me = await client.GetAsync("/api/me");

        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithAnotherTenantsHeader_ReturnsForbidden()
    {
        using var factory = new NobatApiFactory(_database.ConnectionString);
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();

        var ownerSlug = await RegisterClinicAsync(owner);
        var otherSlug = await RegisterClinicAsync(other);

        ownerSlug.ShouldNotBe(otherSlug);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Tenant", otherSlug);

        var me = await owner.SendAsync(request);

        me.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await me.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Type.ShouldBe("tenant.mismatch");
    }

    private static async Task<string> RegisterClinicAsync(HttpClient client)
    {
        var slug = UniqueSlug();

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug,
            clinicName = "مطب تست",
            ownerName = "سپهر",
            phone = OwnerPhone,
            password = Password,
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        return slug;
    }

    private static string UniqueSlug() => "clinic-" + Guid.NewGuid().ToString("N")[..8];

    private sealed record MeResponse(string UserId, string TenantId, string TenantSlug, string FullName, string Role);

    private sealed record SignupOptions(bool AllowPublicSignup);
}
