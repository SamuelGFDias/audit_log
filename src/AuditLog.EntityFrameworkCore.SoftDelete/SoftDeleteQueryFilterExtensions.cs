using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public static class SoftDeleteQueryFilterExtensions
{
    public static ModelBuilder ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeleteEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            if (entityType.IsOwned())
                continue;

            if (entityType.GetDeclaredQueryFilters().Count > 0)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var prop = Expression.Property(parameter, nameof(ISoftDeleteEntity.IsDeleted));
            var condition = Expression.Equal(prop, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }

        return modelBuilder;
    }

    public static ModelBuilder ApplySoftDeleteUniqueIndexFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeleteEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            if (entityType.IsOwned())
                continue;

            var ownUniqueIndexes = entityType.GetIndexes()
                .Concat(
                    entityType.GetNavigations()
                        .Where(n => n.TargetEntityType.IsOwned())
                        .SelectMany(n => n.TargetEntityType.GetIndexes())
                )
                .Where(index => index.IsUnique && index.GetFilter() == null);

            foreach (var index in ownUniqueIndexes)
            {
                index.SetFilter("[IsDeleted] = 0");
            }
        }

        return modelBuilder;
    }
}
