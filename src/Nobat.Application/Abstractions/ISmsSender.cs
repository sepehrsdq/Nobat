namespace Nobat.Application.Abstractions;

/// <summary>
/// Sending a text message. Implementations must never log the full phone number or the code
/// outside development; the console implementation in Infrastructure is development-only.
/// </summary>
public interface ISmsSender
{
    Task SendVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}
