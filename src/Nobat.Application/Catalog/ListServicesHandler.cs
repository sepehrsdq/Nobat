using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;

namespace Nobat.Application.Catalog;

public sealed class ListServicesHandler
{
    private readonly IApplicationDbContext _db;

    public ListServicesHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceDto>> HandleAsync(CancellationToken cancellationToken = default)
        => await _db.Services
            .AsNoTracking()
            .OrderByDescending(service => service.IsActive)
            .ThenBy(service => service.Name)
            .Select(service => new ServiceDto(
                service.Id,
                service.Name,
                service.DurationMinutes,
                service.PriceRial,
                service.IsActive))
            .ToListAsync(cancellationToken);
}
