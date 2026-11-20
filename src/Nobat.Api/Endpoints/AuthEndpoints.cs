using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Nobat.Application.Auth;

namespace Nobat.Api.Endpoints;

public static class AuthEndpoints
{
    public const string TenantClaim = "tenant_id";
    public const string TenantSlugClaim = "tenant_slug";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        auth.MapPost("/register", (Delegate)RegisterAsync);
        auth.MapPost("/login", (Delegate)LoginAsync);
        auth.MapPost("/logout", (Delegate)LogoutAsync);

        app.MapGet("/api/me", Me)
            .WithTags("Auth")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterClinicRequest request,
        RegisterClinicHandler handler,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(request, cancellationToken);
        await SignInAsync(httpContext, session, clock);

        return Results.Created("/api/me", ToResponse(session));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext httpContext,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(request, cancellationToken);
        await SignInAsync(httpContext, session, clock);

        return Results.Ok(ToResponse(session));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static IResult Me(ClaimsPrincipal user) => Results.Ok(new
    {
        userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
        tenantId = user.FindFirstValue(TenantClaim),
        tenantSlug = user.FindFirstValue(TenantSlugClaim),
        fullName = user.FindFirstValue(ClaimTypes.Name),
        role = user.FindFirstValue(ClaimTypes.Role),
    });

    private static async Task SignInAsync(HttpContext httpContext, AuthSession session, TimeProvider clock)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(TenantClaim, session.TenantId.ToString()),
            new Claim(TenantSlugClaim, session.TenantSlug),
            new Claim(ClaimTypes.Name, session.FullName),
            new Claim(ClaimTypes.Role, session.Role.ToString()),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = clock.GetUtcNow().AddHours(12),
            });
    }

    private static object ToResponse(AuthSession session) => new
    {
        userId = session.UserId,
        tenantId = session.TenantId,
        tenantSlug = session.TenantSlug,
        fullName = session.FullName,
        role = session.Role.ToString(),
    };
}
