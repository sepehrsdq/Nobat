namespace Nobat.Application.Auth;

/// <summary>
/// <see cref="AllowPublicSignup"/> is how a public demo stays read-mostly: the demo clinic is
/// seeded, the front door is locked, and the login page says so. Default is on, because a real
/// clinic deployment has to create its own account.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool AllowPublicSignup { get; set; } = true;
}
