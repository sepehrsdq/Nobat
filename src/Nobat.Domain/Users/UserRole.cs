namespace Nobat.Domain.Users;

/// <summary>Panel roles. Patients never have a user row in v1.0.</summary>
public enum UserRole
{
    Owner = 1,
    Secretary = 2,
    Doctor = 3,
}
