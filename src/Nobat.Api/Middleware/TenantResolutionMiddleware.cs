using System.Security.Claims;
using Nobat.Application.Abstractions;

namespace Nobat.Api.Middleware;

/// <summary>
/// Resolves the tenant for the current request and refuses mismatches.
/// The cookie is the source of truth; the <c>X-Tenant</c> header may only confirm it,
/// which keeps a crafted header from ever widening the tenant.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextSetter tenantContext,
        ITenantResolver tenantResolver)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(Endpoints.AuthEndpoints.TenantClaim), out var tenantId)
            || tenantId == Guid.Empty)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "auth.invalid_cookie",
                "نشست معتبر نیست. دوباره وارد شوید.");
            return;
        }

        var headerSlug = context.Request.Headers[TenantHeader].ToString().Trim();

        if (headerSlug.Length > 0)
        {
            var headerTenant = await tenantResolver.FindBySlugAsync(headerSlug, context.RequestAborted);

            if (headerTenant is null)
            {
                await WriteProblemAsync(context, StatusCodes.Status404NotFound, "tenant.not_found",
                    "چنین مطبی وجود ندارد.");
                return;
            }

            if (headerTenant.Id != tenantId)
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "tenant.mismatch",
                    "این نشست به مطب دیگری تعلق دارد.");
                return;
            }
        }

        tenantContext.SetTenant(tenantId);

        await _next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string message)
        => Results.Problem(
                statusCode: statusCode,
                title: message,
                type: code)
            .ExecuteAsync(context);
}
