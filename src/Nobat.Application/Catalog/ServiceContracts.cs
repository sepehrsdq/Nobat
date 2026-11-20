namespace Nobat.Application.Catalog;

public sealed record ServiceDto(Guid Id, string Name, int DurationMinutes, long PriceRial, bool IsActive);

public sealed record CreateServiceRequest(string Name, int DurationMinutes, long PriceRial);

public sealed record UpdateServiceRequest(string Name, int DurationMinutes, long PriceRial, bool IsActive);
