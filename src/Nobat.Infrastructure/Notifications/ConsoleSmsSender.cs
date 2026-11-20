using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nobat.Application.Abstractions;
using Nobat.Application.Notifications;
using Nobat.Domain.Common;

namespace Nobat.Infrastructure.Notifications;

/// <summary>
/// Development stand-in for a real SMS provider: writes the message to the log so the demo works
/// without an account. The code is logged only when <c>Sms:ExposeCodeInResponse</c> is on, the same
/// switch that shows it on screen; otherwise the log just says nothing was delivered.
/// </summary>
public sealed class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;
    private readonly bool _isDemo;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger, IOptions<SmsOptions> options)
    {
        _logger = logger;
        _isDemo = options.Value.ExposeCodeInResponse;
    }

    public Task SendVerificationCodeAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        var masked = IranianPhone.Mask(phoneNumber);

        if (_isDemo)
        {
            _logger.LogInformation("SmsVerificationCode to {Phone}: {Code}", masked, code);
        }
        else
        {
            _logger.LogWarning(
                "No SMS provider is configured; the verification code for {Phone} was not delivered.",
                masked);
        }

        return Task.CompletedTask;
    }
}
