using Microsoft.Extensions.DependencyInjection;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public static class SoftDeleteServiceCollectionExtensions
{
    public static IServiceCollection AddSoftDelete(
        this IServiceCollection services,
        Func<DateTime>? timestampFactory = null)
    {
        services.AddSingleton(sp => new SoftDeleteInterceptor(timestampFactory));
        return services;
    }
}
