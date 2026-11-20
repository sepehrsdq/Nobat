using Microsoft.AspNetCore.Identity;
using Nobat.Application.Abstractions;
using Nobat.Domain.Users;

namespace Nobat.Infrastructure.Security;

/// <summary>
/// Thin adapter over ASP.NET Core's <see cref="PasswordHasher{TUser}"/>: PBKDF2 with the
/// framework's current iteration count, no Identity tables and no hidden queries.
/// </summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly User UnusedUser = null!;

    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(UnusedUser, password);

    public bool Verify(string passwordHash, string password)
        => _hasher.VerifyHashedPassword(UnusedUser, passwordHash, password) != PasswordVerificationResult.Failed;
}
