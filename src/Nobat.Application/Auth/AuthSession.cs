using Nobat.Domain.Users;

namespace Nobat.Application.Auth;

/// <summary>What the API puts in the cookie and returns to the panel after sign-in.</summary>
public sealed record AuthSession(Guid UserId, Guid TenantId, string TenantSlug, string FullName, UserRole Role);
