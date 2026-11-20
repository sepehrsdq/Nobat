using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;
using ServiceEntity = Nobat.Domain.Catalog.Service;

namespace Nobat.Application.Catalog;

public sealed class CreateServiceHandler
{
    private readonly IApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CreateServiceHandler(IApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ServiceDto> HandleAsync(CreateServiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var service = ServiceEntity.Create(
            Guid.Empty,
            request.Name,
            request.DurationMinutes,
            request.PriceRial,
            _clock.GetUtcNow());

        _db.Services.Add(service);
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(service);
    }

    internal static ServiceDto ToDto(ServiceEntity service)
        => new(service.Id, service.Name, service.DurationMinutes, service.PriceRial, service.IsActive);
}
