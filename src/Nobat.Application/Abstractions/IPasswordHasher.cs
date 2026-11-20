namespace Nobat.Application.Abstractions;

/// <summary>Password hashing, kept behind an interface so Application stays free of ASP.NET types.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
