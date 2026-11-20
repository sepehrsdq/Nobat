using Nobat.Application.Abstractions;
using StaffEntity = Nobat.Domain.Personnel.Staff;

namespace Nobat.Application.Personnel;

public sealed class CreateStaffHandler
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CreateStaffHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StaffDto> HandleAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // TenantId stays empty on purpose: the interceptor stamps it from the request tenant.
        var staff = StaffEntity.Create(Guid.Empty, request.DisplayName, request.Title, _clock.GetUtcNow());

        _db.Staff.Add(staff);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(staff);
    }

    internal static StaffDto ToDto(StaffEntity staff)
        => new(staff.Id, staff.DisplayName, staff.Title, staff.IsActive);
}
