using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;

namespace Nobat.Application.Personnel;

public sealed class ListStaffHandler
{
    private readonly IApplicationDbContext _db;

    public ListStaffHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StaffDto>> HandleAsync(CancellationToken cancellationToken = default)
        => await _db.Staff
            .AsNoTracking()
            .OrderByDescending(staff => staff.IsActive)
            .ThenBy(staff => staff.DisplayName)
            .Select(staff => new StaffDto(staff.Id, staff.DisplayName, staff.Title, staff.IsActive))
            .ToListAsync(cancellationToken);
}
