using Microsoft.EntityFrameworkCore;
using Nobat.Application.Abstractions;
using Nobat.Domain.Common;

namespace Nobat.Application.Catalog;

public sealed class UpdateServiceHandler
{
    private readonly IApplicationDbContext _db;

    public UpdateServiceHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceDto> HandleAsync(
        Guid id,
        UpdateServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var service = await _db.Services.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new DomainRuleViolationException("service.not_found", "خدمت پیدا نشد.");

        service.Rename(request.Name);
        service.ChangeDuration(request.DurationMinutes);
        service.ChangePrice(request.PriceRial);

        if (request.IsActive)
        {
            service.Activate();
        }
        else
        {
            service.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);

        return CreateServiceHandler.ToDto(service);
    }
}
