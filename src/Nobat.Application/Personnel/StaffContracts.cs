namespace Nobat.Application.Personnel;

public sealed record StaffDto(Guid Id, string DisplayName, string? Title, bool IsActive);

public sealed record CreateStaffRequest(string DisplayName, string? Title);

public sealed record UpdateStaffRequest(string DisplayName, string? Title, bool IsActive);
