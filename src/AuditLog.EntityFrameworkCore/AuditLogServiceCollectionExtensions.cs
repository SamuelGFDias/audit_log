using AuditLog.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLog.EntityFrameworkCore;

public static class AuditLogServiceCollectionExtensions
{
    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        Action<AuditRegistry>? configureRegistry = null)
    {
        var registry = new AuditRegistry();
        configureRegistry?.Invoke(registry);

        services.AddSingleton(registry);
        services.AddSingleton<AuditSaveInterceptor>();

        return services;
    }

    public static IServiceCollection AddAuditLogging(
        this IServiceCollection services,
        AuditRegistry registry)
    {
        services.AddSingleton(registry);
        services.AddSingleton<AuditSaveInterceptor>();

        return services;
    }
}
