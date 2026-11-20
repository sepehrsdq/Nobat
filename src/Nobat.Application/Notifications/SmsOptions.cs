namespace Nobat.Application.Notifications;

/// <summary>
/// SMS configuration. <see cref="ExposeCodeInResponse"/> exists so a local demo can show the code
/// without an SMS provider; it must stay false anywhere a real patient can reach.
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public bool ExposeCodeInResponse { get; set; }
}
