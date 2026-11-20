using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Application.Personnel;

public sealed class UpdateStaffHandler
{
    private readonly IApplicationDbContext _db;

    public UpdateStaffHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StaffDto> HandleAsync(
        Guid id,
        UpdateStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A staff member of another tenant is invisible here, so this is a 404, not a 403.
        var staff = await _db.Staff.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new DomainRuleViolationException("staff.not_found", "پرسنل پیدا نشد.");

        staff.Rename(request.DisplayName);
        staff.ChangeTitle(request.Title);

        if (request.IsActive)
        {
            staff.Activate();
        }
        else
        {
            staff.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return CreateStaffHandler.ToDto(staff);
    }
}
