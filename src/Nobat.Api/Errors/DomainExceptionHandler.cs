using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nobat.Domain.Common;

namespace Nobat.Api.Errors;

/// <summary>
/// Turns expected failures into ProblemDetails with a stable code.
/// Domain rejections get their own code; a body the serializer cannot read gets
/// <c>request.invalid</c> instead of a 500. Anything else falls through to the generic 500 handler.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DomainRuleViolationException domainException)
        {
            await WriteProblemAsync(
                httpContext,
                StatusFor(domainException.Code),
                domainException.Code,
                domainException.Message,
                detail: null,
                cancellationToken);

            return true;
        }

        // Unknown or mistyped JSON members throw while binding the body. They are client mistakes,
        // not server faults, and the offset/path in the message tells the caller what to fix.
        if (exception is JsonException or BadHttpRequestException)
        {
            var statusCode = exception is BadHttpRequestException { StatusCode: >= 400 and < 500 } badRequest
                ? badRequest.StatusCode
                : StatusCodes.Status400BadRequest;

            await WriteProblemAsync(
                httpContext,
                statusCode,
                "request.invalid",
                "بدنهٔ درخواست خوانده نشد. نام و نوع فیلدها را بررسی کنید.",
                (exception as BadHttpRequestException)?.InnerException?.Message ?? exception.Message,
                cancellationToken);

            return true;
        }

        return false;
    }

    private static int StatusFor(string code) => code switch
    {
        "auth.invalid_credentials" => StatusCodes.Status401Unauthorized,
        "auth.invalid_cookie" => StatusCodes.Status401Unauthorized,
        "tenant.slug_taken" => StatusCodes.Status409Conflict,
        "tenant.not_found" => StatusCodes.Status404NotFound,
        "staff.not_found" => StatusCodes.Status404NotFound,
        "service.not_found" => StatusCodes.Status404NotFound,
        "appointment.slot_taken" => StatusCodes.Status409Conflict,
        "otp.throttled" => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status400BadRequest,
    };

    private static Task WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string title,
        string? detail,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = statusCode;

        return httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Type = code,
                Detail = detail,
            },
            cancellationToken);
    }
}
