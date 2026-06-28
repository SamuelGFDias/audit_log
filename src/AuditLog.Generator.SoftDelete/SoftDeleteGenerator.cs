#nullable enable
using AuditLog.Generator;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AuditLog.Generator.SoftDelete;

[Generator]
public sealed class SoftDeleteGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var dbContexts = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => EntityDetector.IsDbContextCandidate(node),
                transform: static (ctx, _) => EntityDetector.GetDbContextTarget(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        context.RegisterSourceOutput(dbContexts, static (ctx, infos) => GenerateAll(ctx, infos));
    }

    private static void GenerateAll(
        SourceProductionContext context,
        ImmutableArray<DbContextInfo> dbContexts)
    {
        if (dbContexts.Length == 0) return;

        // Deduplicate entities across all DbContexts (same full name = same entity)
        var seenEntities = new HashSet<string>();
        var allEntities = ImmutableArray.CreateBuilder<EntityInfo>();

        foreach (var dbCtx in dbContexts)
        {
            foreach (var entity in dbCtx.Entities)
            {
                if (seenEntities.Add(entity.FullName))
                {
                    var fks = ImmutableArray.CreateBuilder<RelationshipInfo>();
                    fks.AddRange(entity.ReferencingFks);

                    foreach (var otherCtx in dbContexts)
                    {
                        foreach (var other in otherCtx.Entities)
                        {
                            if (other.FullName == entity.FullName) continue;
                            foreach (var fk in other.OwnFkProperties)
                            {
                                if (fk.TargetEntityName == entity.Name ||
                                    fk.TargetEntityName == GetShortName(entity.FullName))
                                {
                                    bool exists = false;
                                    foreach (var existing in fks)
                                    {
                                        if (existing.DependentEntityFullName == other.FullName &&
                                            existing.FkPropertyName == fk.PropertyName)
                                        { exists = true; break; }
                                    }
                                    if (!exists)
                                    {
                                        fks.Add(new RelationshipInfo(
                                            other.FullName, other.Name,
                                            fk.PropertyName, fk.PropertyType,
                                            entity.PrimaryKeyName, fk.IsNullable,
                                            fk.DeleteBehavior, false, other.IsSoftDelete));
                                    }
                                }
                            }
                        }
                    }

                    allEntities.Add(new EntityInfo(
                        entity.Namespace, entity.Name, entity.FullName,
                        entity.PrimaryKeyType, entity.PrimaryKeyName, entity.IsSoftDelete,
                        entity.OwnFkProperties, fks.ToImmutable()));
                }
            }
        }

        var finalEntities = allEntities.ToImmutable();

        foreach (var entity in finalEntities)
        {
            if (!entity.IsSoftDelete) continue;
            HandlerGenerator.GenerateHandlerClass(context, entity, dbContexts[0].FullName);
        }

        RegistryGenerator.GenerateRegistryExtension(context, finalEntities, dbContexts[0].FullName);
    }

    private static string GetShortName(string fullName)
    {
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
    }
}
