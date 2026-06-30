#nullable enable
using AuditLog.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AuditLog.Generator.SoftDelete;

internal static class EntityDetector
{
    public const string ISoftDeleteEntityInterface = "AuditLog.EntityFrameworkCore.SoftDelete.ISoftDeleteEntity";
    public const string GenerateSoftDeleteAttribute = "AuditLog.EntityFrameworkCore.SoftDelete.GenerateSoftDeleteAttribute";

    public static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    public static bool IsDbContextCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0, BaseList: not null };
    }

    public static (DbContextInfo? dbContext, ImmutableArray<RelationshipConfig> relationships) GetDbContextTarget(
        GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (typeSymbol is null) return (null, ImmutableArray<RelationshipConfig>.Empty);

        if (!typeSymbol.HasAttribute(GenerateSoftDeleteAttribute)) return (null, ImmutableArray<RelationshipConfig>.Empty);

        var baseType = typeSymbol.BaseType;
        if (baseType is null || baseType.Name != "DbContext") return (null, ImmutableArray<RelationshipConfig>.Empty);

        var entities = new List<EntityInfo>();
        var entityLookup = new Dictionary<string, (ITypeSymbol type, string fullName)>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Type is not INamedTypeSymbol { Name: "DbSet", IsGenericType: true } dbSet) continue;
            if (dbSet.TypeArguments.Length == 0) continue;

            var entityType = dbSet.TypeArguments[0];
            if (entityType is INamedTypeSymbol entityNamed)
            {
                var info = AnalyzeEntity(entityType);
                if (info is not null)
                {
                    entities.Add(info);
                    entityLookup[info.Name] = (entityType, info.FullName);
                }
            }
        }

        if (entities.Count == 0) return (null, ImmutableArray<RelationshipConfig>.Empty);

        var relationships = FluentApiParser.ParseOnModelCreating(classDecl, typeSymbol, entities.ToImmutableArray(), context);

        var entityMap = entities.ToDictionary(e => e.Name, e => e);

        var updatedEntities = entities.Select(e =>
        {
            var referencingFks = relationships
                .Where(r => r.PrincipalEntity == e.Name && !r.IsOwnership)
                .Select(r => new RelationshipInfo(
                    r.DependentEntityFullName,
                    r.DependentEntityName,
                    r.FkPropertyName,
                    r.FkPropertyType,
                    e.PrimaryKeyName,
                    r.FkIsNullable,
                    r.DeleteBehavior,
                    false,
                    r.DependentIsSoftDelete))
                .ToImmutableArray();

            return new EntityInfo(
                e.Namespace, e.Name, e.FullName,
                e.PrimaryKeyType, e.PrimaryKeyName, e.IsSoftDelete,
                ImmutableArray<FkProperty>.Empty,
                referencingFks);
        }).ToImmutableArray();

        var dbContext = new DbContextInfo(
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            typeSymbol.ToDisplayString(),
            updatedEntities);

        return (dbContext, relationships);
    }

    public static EntityInfo? AnalyzeEntity(ITypeSymbol typeSymbol)
    {
        var isSoftDelete = typeSymbol.ImplementsInterface(ISoftDeleteEntityInterface);

        var ns = typeSymbol.ContainingNamespace.ToDisplayString();
        var fullName = typeSymbol.ToDisplayString();
        var name = typeSymbol.Name;

        var pkName = "Id";
        var pkType = "global::System.Guid";

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (!prop.Name.Equals("Id") && !prop.Name.Equals(name + "Id")) continue;

            pkName = prop.Name;
            pkType = GetFullyQualifiedTypeName(prop.Type);
            break;
        }

        return new EntityInfo(ns, name, fullName, pkType, pkName, isSoftDelete,
            ImmutableArray<FkProperty>.Empty, ImmutableArray<RelationshipInfo>.Empty);
    }

    internal static string GetFullyQualifiedTypeName(ITypeSymbol type)
    {
        var name = type.ToDisplayString();
        if (name == "Guid" || name == "System.Guid") return "global::System.Guid";
        if (name == "int" || name == "System.Int32") return "int";
        if (name == "long" || name == "System.Int64") return "long";
        if (name == "string" || name == "System.String") return "string";
        if (name == "bool" || name == "System.Boolean") return "bool";
        if (name == "DateTime" || name == "System.DateTime") return "global::System.DateTime";
        return name;
    }
}
