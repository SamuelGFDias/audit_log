using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public static class SoftDeleteCascadeHandler
{
    public static async Task HandleDeleteAsync(
        DbContext context,
        EntityEntry entry,
        DateTime deletedAt)
        => await HandleDeleteAsync(context, entry, deletedAt, new HashSet<object>());

    private static async Task HandleDeleteAsync(
        DbContext context,
        EntityEntry entry,
        DateTime deletedAt,
        HashSet<object> visited)
    {
        if (!visited.Add(entry.Entity))
            return;

        MarkSoftDeleted(entry, deletedAt);

        foreach (var fk in entry.Metadata.GetReferencingForeignKeys().Where(ShouldProcessForeignKey))
        {
            switch (fk.DeleteBehavior)
            {
                case DeleteBehavior.Restrict:
                case DeleteBehavior.NoAction:
                    await ValidateRestrictAsync(context, entry, fk);
                    break;

                case DeleteBehavior.Cascade:
                    await ProcessCascadeAsync(context, entry, deletedAt, fk, visited);
                    break;

                case DeleteBehavior.SetNull:
                case DeleteBehavior.ClientSetNull:
                    await ProcessSetNullAsync(context, entry, fk);
                    break;
            }
        }
    }

    private static void MarkSoftDeleted(EntityEntry entry, DateTime deletedAt)
    {
        if (entry.State == EntityState.Modified && (bool)entry.Property("IsDeleted").CurrentValue!)
            return;

        if (entry.State == EntityState.Deleted)
        {
            entry.State = EntityState.Unchanged;
        }

        entry.Property("IsDeleted").CurrentValue = true;
        entry.Property("IsDeleted").IsModified = true;

        entry.Property("DeletedAt").CurrentValue = deletedAt;
        entry.Property("DeletedAt").IsModified = true;

        BlindOwnedEntities(entry);
    }

    private static void BlindOwnedEntities(EntityEntry currentEntry)
    {
        foreach (var navigation in currentEntry.Navigations)
        {
            if (navigation.Metadata is not INavigation navMetadata || !navMetadata.TargetEntityType.IsOwned())
                continue;

            if (navigation.CurrentValue == null)
                continue;

            var ownedEntry = currentEntry.Context.Entry(navigation.CurrentValue);
            ownedEntry.State = EntityState.Unchanged;

            BlindOwnedEntities(ownedEntry);
        }
    }

    private static async Task ValidateRestrictAsync(
        DbContext context,
        EntityEntry entry,
        IForeignKey fk)
    {
        var existsDependents = await ExistsDependentsAsync(context, entry, fk);
        if (existsDependents)
        {
            var pkValues = GetPrimaryKeyValues(entry);
            throw new RestrictDeleteViolationException(entry.Metadata.Name, pkValues);
        }
    }

    private static async Task ProcessCascadeAsync(
        DbContext context,
        EntityEntry entry,
        DateTime deletedAt,
        IForeignKey fk,
        HashSet<object> visited)
    {
        var dependents = await LoadDependentsAsync(context, entry, fk);

        foreach (var dep in dependents)
        {
            var depEntry = context.Entry(dep);
            var dependentClrType = depEntry.Metadata.ClrType;

            var hasSoftDelete = typeof(ISoftDeleteEntity).IsAssignableFrom(dependentClrType)
                                && depEntry.Metadata.FindProperty(nameof(ISoftDeleteEntity.IsDeleted)) != null;

            if (hasSoftDelete)
            {
                MarkSoftDeleted(depEntry, deletedAt);
                await HandleDeleteAsync(context, depEntry, deletedAt, visited);
            }
            else
            {
                depEntry.State = EntityState.Deleted;
            }
        }
    }

    private static async Task ProcessSetNullAsync(
        DbContext context,
        EntityEntry entry,
        IForeignKey fk)
    {
        var dependents = await LoadDependentsAsync(context, entry, fk);
        foreach (var dep in dependents)
        {
            var depEntry = context.Entry(dep);
            foreach (var prop in fk.Properties)
            {
                depEntry.Property(prop.Name).CurrentValue = null;
                depEntry.Property(prop.Name).IsModified = true;
            }

            depEntry.State = EntityState.Modified;
        }
    }

    private static async Task<bool> ExistsDependentsAsync(
        DbContext context,
        EntityEntry entry,
        IForeignKey fk)
    {
        var clrType = fk.DeclaringEntityType.ClrType;
        var dbSet = GetDynamicQueryable(context, clrType);

        var ignoreFiltersMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m is
                        {
                            Name: nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters),
                            IsGenericMethodDefinition: true
                        }
                        && m.GetParameters().Length == 1
            )
            .MakeGenericMethod(clrType);

        var query = ignoreFiltersMethod?.Invoke(null, [dbSet]) ?? dbSet;
        var predicate = BuildPredicate(entry, fk);

        return await InvokeAnyAsync(fk.DeclaringEntityType.ClrType, query, predicate);
    }

    private static async Task<List<object>> LoadDependentsAsync(
        DbContext context,
        EntityEntry entry,
        IForeignKey fk)
    {
        var clrType = fk.DeclaringEntityType.ClrType;
        var dbSet = GetDynamicQueryable(context, clrType);
        var predicate = BuildPredicate(entry, fk);

        var whereMethod = typeof(Queryable)
            .GetMethods()
            .First(m => m is { Name: nameof(Queryable.Where), IsGenericMethodDefinition: true }
                        && m.GetParameters().Length == 2
            )
            .MakeGenericMethod(clrType);

        var filtered = whereMethod.Invoke(null, [dbSet, predicate])!;

        var toListAsync = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync))
            .Select(m => m.MakeGenericMethod(clrType))
            .First(m => m.GetParameters().Length == 2)
            .Invoke(null, [filtered, CancellationToken.None])!;

        await ((Task)toListAsync).ConfigureAwait(false);
        var result = ((dynamic)toListAsync).Result as IEnumerable<object>;
        return result?.ToList() ?? [];
    }

    private static LambdaExpression BuildPredicate(
        EntityEntry entry,
        IForeignKey fk)
    {
        var keyValues = GetPrimaryKeyValues(entry);
        var param = Expression.Parameter(fk.DeclaringEntityType.ClrType, "e");
        Expression? body = null;

        for (var i = 0; i < fk.Properties.Count; i++)
        {
            var prop = fk.Properties[i];
            var left = BuildPropertyAccess(param, prop);
            var right = Expression.Constant(keyValues[i], prop.ClrType);
            var eq = Expression.Equal(left, right);
            body = body == null ? eq : Expression.AndAlso(body, eq);
        }

        return Expression.Lambda(body!, param);
    }

    private static bool ShouldProcessForeignKey(IForeignKey fk)
        => !fk.IsOwnership
           && !fk.DeclaringEntityType.IsOwned()
           && !fk.PrincipalEntityType.IsOwned();

    private static Expression BuildPropertyAccess(ParameterExpression param, IProperty property)
    {
        if (property.PropertyInfo != null)
            return Expression.Property(param, property.PropertyInfo);

        var efPropertyMethod = typeof(EF)
            .GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(property.ClrType);

        return Expression.Call(
            efPropertyMethod,
            param,
            Expression.Constant(property.Name)
        );
    }

    private static object[] GetPrimaryKeyValues(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey()!;
        return pk.Properties
            .Select(p => entry.Property(p.Name).CurrentValue!)
            .ToArray();
    }

    private static IQueryable GetDynamicQueryable(DbContext context, Type clrType)
    {
        return (IQueryable)context.GetType()
            .GetMethod(nameof(DbContext.Set), [])?
            .MakeGenericMethod(clrType)
            .Invoke(context, null)!;
    }

    private static Task<bool> InvokeAnyAsync(
        Type clrType,
        object dbSet,
        LambdaExpression predicate)
    {
        var anyMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync))
            .Select(m => m.MakeGenericMethod(clrType))
            .First(m => m.GetParameters().Length == 3);

        return (Task<bool>)anyMethod.Invoke(
            null,
            [dbSet, predicate, CancellationToken.None]
        )!;
    }
}
