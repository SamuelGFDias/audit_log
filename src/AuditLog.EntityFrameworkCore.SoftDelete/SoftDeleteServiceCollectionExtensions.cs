using Microsoft.Extensions.DependencyInjection;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public static class SoftDeleteServiceCollectionExtensions
{
    public static IServiceCollection AddSoftDelete(
        this IServiceCollection services,
        SoftDeleteHandlerRegistry? registry = null,
        Func<DateTime>? timestampFactory = null)
    {
        services.AddSingleton(sp => new SoftDeleteInterceptor(registry, timestampFactory));
        return services;
    }
}
