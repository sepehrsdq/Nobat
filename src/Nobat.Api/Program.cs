using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Nobat.Api.Endpoints;
using Nobat.Api.Errors;
using Nobat.Api.Middleware;
using Nobat.Application;
using Nobat.Infrastructure;
using Nobat.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default");

if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Connection string 'Default' is missing. Set the ConnectionStrings__Default environment variable.");
    }

    // Development convenience only: LocalDB with a trusted connection, no secret involved.
    connectionString = LocalDbDefaults.ConnectionString;
}

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());

    // A misspelled property ("day" instead of "dayOfWeek") must fail loudly.
    // Silently defaulting an enum to Sunday once already produced wrong opening hours.
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "nobat.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;

        // This is an API: answer 401/403 instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    options.AddPolicy("public", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapStaffEndpoints();
app.MapServiceEndpoints();
app.MapWorkingHourEndpoints();
app.MapPublicEndpoints();

app.MapGet("/health", (TimeProvider clock) => Results.Ok(new HealthResponse("ok", clock.GetUtcNow())))
    .WithName("Health");

// Single image: unknown paths fall back to the SPA, unknown API paths stay API errors.
app.MapFallback((HttpContext context) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "مسیر پیدا نشد.");
    }

    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");

    return File.Exists(indexPath)
        ? Results.File(indexPath, "text/html")
        : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "فرانت هنوز build نشده است.");
});

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset Utc);

public partial class Program;
