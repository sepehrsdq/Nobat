using System.Net;
using System.Net.Http.Json;

namespace Nobat.IntegrationTests.Infrastructure;

internal static class ApiHelpers
{
    public const string OwnerPhone = "09121234567";
    public const string Password = "Nobat1405";

    public static async Task<string> RegisterClinicAsync(HttpClient client, string? clinicName = null)
    {
        var slug = "clinic-" + Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            slug,
            clinicName = clinicName ?? "مطب تست",
            ownerName = "سپهر",
            phone = OwnerPhone,
            password = Password,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return slug;
    }
}
