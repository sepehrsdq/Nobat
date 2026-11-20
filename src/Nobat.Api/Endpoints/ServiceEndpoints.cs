using Nobat.Application.Catalog;

namespace Nobat.Api.Endpoints;

public static class ServiceEndpoints
{
    public static IEndpointRouteBuilder MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services")
            .WithTags("Services")
            .RequireAuthorization();

        group.MapGet("", async (ListServicesHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(cancellationToken)));

        group.MapPost("", async (
            CreateServiceRequest request,
            CreateServiceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var service = await handler.HandleAsync(request, cancellationToken);
            return Results.Created($"/api/services/{service.Id}", service);
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateServiceRequest request,
            UpdateServiceHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(id, request, cancellationToken)));

        return app;
    }
}
