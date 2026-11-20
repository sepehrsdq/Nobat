using Microsoft.Extensions.DependencyInjection;
using Nobat.Application.Appointments;
using Nobat.Application.Auth;
using Nobat.Application.Catalog;
using Nobat.Application.Personnel;
using Nobat.Application.Public;
using Nobat.Application.WorkingHours;

namespace Nobat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterClinicHandler>();
        services.AddScoped<LoginHandler>();

        services.AddScoped<CreateStaffHandler>();
        services.AddScoped<ListStaffHandler>();
        services.AddScoped<UpdateStaffHandler>();

        services.AddScoped<CreateServiceHandler>();
        services.AddScoped<ListServicesHandler>();
        services.AddScoped<UpdateServiceHandler>();

        services.AddScoped<GetWorkingHoursHandler>();
        services.AddScoped<ReplaceWorkingHoursHandler>();

        services.AddScoped<GetPublicClinicHandler>();
        services.AddScoped<GetPublicSlotsHandler>();
        services.AddScoped<RequestOtpHandler>();
        services.AddScoped<BookAppointmentHandler>();

        services.AddScoped<ListAppointmentsHandler>();
        services.AddScoped<CancelAppointmentHandler>();
        services.AddScoped<RescheduleAppointmentHandler>();
        services.AddScoped<GetRescheduleSlotsHandler>();

        return services;
    }
}
