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

        var compilation = context.SemanticModel.Compilation;
        var entityTypesFromDbSet = DiscoverEntitiesFromDbSets(typeSymbol);

        List<EntityInfo> entities;
        Dictionary<string, (ITypeSymbol type, string fullName)> entityLookup;

        if (entityTypesFromDbSet.Count > 0)
        {
            entities = entityTypesFromDbSet.Select(e => AnalyzeEntity(e.type)!).Where(e => e is not null).ToList()!;
            entityLookup = entityTypesFromDbSet.ToDictionary(e => e.type.Name, e => e);
        }
        else
        {
            (entities, entityLookup) = DiscoverEntitiesFromOnModelCreating(classDecl, compilation);
        }

        if (entities.Count == 0) return (null, ImmutableArray<RelationshipConfig>.Empty);

        var relationships = FluentApiParser.ParseOnModelCreating(classDecl, typeSymbol, entities.ToImmutableArray(), entityLookup, context);

        var updatedEntities = entities.Select(e =>
        {
            var referencingFks = relationships
                .Where(r => r.PrincipalEntity == e.Name && !r.IsOwnership && FkMatchesPrincipal(r, e.Name))
                .Select(r => new RelationshipInfo(
                    r.DependentEntityFullName,
                    r.DependentEntityName,
                    r.DependentPkName,
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

    private static List<(ITypeSymbol type, string fullName)> DiscoverEntitiesFromDbSets(INamedTypeSymbol typeSymbol)
    {
        var result = new List<(ITypeSymbol type, string fullName)>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Type is not INamedTypeSymbol { Name: "DbSet", IsGenericType: true } dbSet) continue;
            if (dbSet.TypeArguments.Length == 0) continue;

            var entityType = dbSet.TypeArguments[0];
            if (entityType is INamedTypeSymbol)
            {
                result.Add((entityType, entityType.ToDisplayString()));
            }
        }
        return result;
    }

    private static (List<EntityInfo> entities, Dictionary<string, (ITypeSymbol type, string fullName)> entityLookup)
        DiscoverEntitiesFromOnModelCreating(ClassDeclarationSyntax classDecl, Compilation compilation)
    {
        var onModelCreating = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

        if (onModelCreating?.Body is null)
            return (new List<EntityInfo>(), new Dictionary<string, (ITypeSymbol type, string fullName)>());

        var seen = new HashSet<string>();
        var symbols = new List<ITypeSymbol>();
        var entityLookup = new Dictionary<string, (ITypeSymbol type, string fullName)>();
        var classSemanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);

        foreach (var statement in onModelCreating.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax exprStmt) continue;
            if (exprStmt.Expression is not InvocationExpressionSyntax invocation) continue;
            if (invocation.Expression is not MemberAccessExpressionSyntax ma) continue;

            if (ma.Name is GenericNameSyntax { Identifier: { Text: "Entity" } } genericName)
            {
                var entityName = genericName.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();
                if (entityName is null) continue;
                var typeInfo = classSemanticModel.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]);
                var typeSymbol = typeInfo.Type;
                if (typeSymbol is not null && seen.Add(typeSymbol.ToDisplayString()))
                {
                    symbols.Add(typeSymbol);
                    entityLookup[typeSymbol.Name] = (typeSymbol, typeSymbol.ToDisplayString());
                }
            }
            else if (ma.Name.Identifier.Text == "ApplyConfiguration")
            {
                var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                if (arg is not ObjectCreationExpressionSyntax creation) continue;
                var mapTypeName = creation.Type.ToString();
                var mapClass = FindClassDeclaration(compilation, mapTypeName);
                if (mapClass is null) continue;
                var mapSemanticModel = compilation.GetSemanticModel(mapClass.SyntaxTree);
                var entityTypeSymbol = FluentApiParser.ExtractEntityTypeFromSemanticModel(mapClass, mapSemanticModel);
                if (entityTypeSymbol is null) continue;
                var key = entityTypeSymbol.ToDisplayString();
                if (seen.Add(key))
                {
                    symbols.Add(entityTypeSymbol);
                    entityLookup[entityTypeSymbol.Name] = (entityTypeSymbol, key);
                }
            }
            else if (ma.Name.Identifier.Text == "ApplyConfigurationsFromAssembly")
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var root = tree.GetRoot();
                    var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                    var treeSemanticModel = compilation.GetSemanticModel(tree);

                    foreach (var cls in classes)
                    {
                        var entityTypeSymbol = FluentApiParser.ExtractEntityTypeFromSemanticModel(cls, treeSemanticModel);
                        if (entityTypeSymbol is null) continue;
                        var key = entityTypeSymbol.ToDisplayString();
                        if (!seen.Add(key)) continue;
                        symbols.Add(entityTypeSymbol);
                        entityLookup[entityTypeSymbol.Name] = (entityTypeSymbol, key);
                    }
                }
            }
        }

        var entities = symbols.Select(s => AnalyzeEntity(s))
            .Where(e => e is not null)
            .Cast<EntityInfo>()
            .ToList();

        return (entities, entityLookup);
    }

    private static ClassDeclarationSyntax? FindClassDeclaration(Compilation compilation, string className)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var found = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (found is not null)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var symbol = semanticModel.GetDeclaredSymbol(found);
                if (symbol?.ToDisplayString() == className || symbol?.ToDisplayString().EndsWith("." + className) == true)
                    return found;
                return found;
            }
        }
        return null;
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

    internal static bool FkMatchesPrincipal(RelationshipConfig r, string principalName)
    {
        if (r.FkPropertyName == principalName) return true;
        if (r.FkPropertyName == principalName + "Id") return true;
        if (r.FkPropertyName.StartsWith(principalName + "_")) return true;
        return false;
    }
}
