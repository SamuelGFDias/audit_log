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
            .Where(static m => m.dbContext is not null)
            .Select(static (m, _) => (m.dbContext!, m.relationships))
            .Collect();

        context.RegisterSourceOutput(dbContexts, static (ctx, infos) => GenerateAll(ctx, infos));
    }

    private static void GenerateAll(
        SourceProductionContext context,
        ImmutableArray<(DbContextInfo dbContext, ImmutableArray<RelationshipConfig> relationships)> dbContexts)
    {
        if (dbContexts.Length == 0) return;

        var seenEntities = new HashSet<string>();
        var allEntities = ImmutableArray.CreateBuilder<EntityInfo>();

        foreach (var (dbCtx, rels) in dbContexts)
        {
            foreach (var entity in dbCtx.Entities)
            {
                if (seenEntities.Add(entity.FullName))
                {
                    var referencingFks = rels
                        .Where(r => r.PrincipalEntity == entity.Name && !r.IsOwnership && EntityDetector.FkMatchesPrincipal(r, entity.Name))
                        .Select(r => new RelationshipInfo(
                            r.DependentEntityFullName,
                            r.DependentEntityName,
                            r.DependentPkName,
                            r.FkPropertyName,
                            r.FkPropertyType,
                            entity.PrimaryKeyName,
                            r.FkIsNullable,
                            r.DeleteBehavior,
                            false,
                            r.DependentIsSoftDelete))
                        .ToImmutableArray();

                    allEntities.Add(new EntityInfo(
                        entity.Namespace, entity.Name, entity.FullName,
                        entity.PrimaryKeyType, entity.PrimaryKeyName, entity.IsSoftDelete,
                        ImmutableArray<FkProperty>.Empty,
                        referencingFks));
                }
            }
        }

        var finalEntities = allEntities.ToImmutable();

        foreach (var entity in finalEntities)
        {
            if (!entity.IsSoftDelete) continue;
            HandlerGenerator.GenerateHandlerClass(context, entity, dbContexts[0].dbContext.FullName);
        }

        RegistryGenerator.GenerateRegistryExtension(context, finalEntities, dbContexts[0].dbContext.FullName);
    }
}
