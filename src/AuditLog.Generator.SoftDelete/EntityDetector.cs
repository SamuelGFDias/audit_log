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
    public const string DeleteBehaviorAttr = "AuditLog.EntityFrameworkCore.SoftDelete.DeleteBehaviorAttribute";

    public static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    public static bool IsDbContextCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0, BaseList: not null };
    }

    public static DbContextInfo? GetDbContextTarget(GeneratorSyntaxContext context)
    {
        var typeSymbol = GetTypeSymbol(context);
        if (typeSymbol is null) return null;
        if (!typeSymbol.HasAttribute(GenerateSoftDeleteAttribute)) return null;

        var baseType = typeSymbol.BaseType;
        if (baseType is null || baseType.Name != "DbContext") return null;

        var entities = new List<EntityInfo>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Type is not INamedTypeSymbol { Name: "DbSet", IsGenericType: true } dbSet) continue;
            if (dbSet.TypeArguments.Length == 0) continue;

            var entityType = dbSet.TypeArguments[0];
            if (entityType is INamedTypeSymbol entityNamed)
            {
                var info = AnalyzeEntity(context, entityType);
                if (info is not null)
                    entities.Add(info);
            }
        }

        return new DbContextInfo(
            typeSymbol.ContainingNamespace.ToDisplayString(),
            typeSymbol.Name,
            typeSymbol.ToDisplayString(),
            entities.ToImmutableArray());
    }

    private static INamedTypeSymbol? GetTypeSymbol(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl) return null;
        return context.SemanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
    }

    public static EntityInfo? AnalyzeEntity(GeneratorSyntaxContext context, ITypeSymbol typeSymbol)
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

        var ownFks = new List<FkProperty>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            var propName = prop.Name;
            if (propName == pkName) continue;

            if (propName.EndsWith("Id"))
            {
                var targetName = propName.Substring(0, propName.Length - 2);
                var behavior = InferDeleteBehavior(prop, typeSymbol);
                ownFks.Add(new FkProperty(
                    propName,
                    GetFullyQualifiedTypeName(prop.Type),
                    prop.Type.NullableAnnotation == NullableAnnotation.Annotated,
                    targetName,
                    behavior));
            }
        }

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol navProp) continue;
            if (!HasCollectionType(navProp.Type)) continue;

            var elementType = RoslynExtensions.GetCollectionElementType(navProp.Type);
            if (elementType is null) continue;

            var hasBehaviorAttr = navProp.HasAttribute(DeleteBehaviorAttr);
            if (!hasBehaviorAttr) continue;

            var behavior = ExtractDeleteBehaviorFromAttr(navProp);
            if (behavior is null) continue;

            var fkOnTarget = FindFkOnTarget(elementType, typeSymbol);
            if (fkOnTarget is null) continue;

            ownFks.Add(new FkProperty(
                fkOnTarget.Name,
                GetFullyQualifiedTypeName(fkOnTarget.Type),
                fkOnTarget.Type.NullableAnnotation == NullableAnnotation.Annotated,
                typeSymbol.Name,
                behavior));
        }

        return new EntityInfo(ns, name, fullName, pkType, pkName, isSoftDelete, ownFks.ToImmutableArray());
    }

    private static string InferDeleteBehavior(IPropertySymbol fkProperty, ITypeSymbol entityType)
    {
        if (fkProperty.HasAttribute(DeleteBehaviorAttr))
        {
            var behavior = ExtractDeleteBehaviorFromAttr(fkProperty);
            if (behavior is not null) return behavior;
        }

        foreach (var member in entityType.GetMembers())
        {
            if (member is not IPropertySymbol navProp) continue;
            if (!HasCollectionType(navProp.Type)) continue;

            var elementType = RoslynExtensions.GetCollectionElementType(navProp.Type);
            if (elementType is null) continue;

            var fkName = fkProperty.Name;
            var relatedName = fkName.EndsWith("Id") ? fkName.Substring(0, fkName.Length - 2) : "";

            if (elementType.Name == relatedName)
            {
                if (navProp.HasAttribute(DeleteBehaviorAttr))
                {
                    var behavior = ExtractDeleteBehaviorFromAttr(navProp);
                    if (behavior is not null) return behavior;
                }
            }
        }

        if (fkProperty.Type.NullableAnnotation == NullableAnnotation.Annotated)
            return "SetNull";

        return "Restrict";
    }

    private static string? ExtractDeleteBehaviorFromAttr(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != DeleteBehaviorAttr) continue;
            if (attr.ConstructorArguments.Length == 0) continue;
            var value = attr.ConstructorArguments[0].Value;
            if (value is int intVal)
            {
                return intVal switch
                {
                    0 => "Cascade",
                    1 => "Restrict",
                    2 => "SetNull",
                    _ => null
                };
            }
        }
        return null;
    }

    private static bool HasCollectionType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return false;
        foreach (var iface in named.AllInterfaces)
        {
            if (iface.Name is "IEnumerable" or "ICollection" or "IList")
                return true;
        }
        return false;
    }

    private static IPropertySymbol? FindFkOnTarget(ITypeSymbol targetType, ITypeSymbol principalType)
    {
        var principalName = principalType.Name;
        foreach (var member in targetType.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Name == principalName + "Id" || prop.Name == principalName + "_Id")
                return prop;
        }
        var pkProp = GetPrimaryKeyProperty(principalType);
        if (pkProp is null) return null;
        foreach (var member in targetType.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (!prop.Name.EndsWith("Id")) continue;
            if (SymbolEqualityComparer.Default.Equals(prop.Type, pkProp.Type))
                return prop;
        }
        return null;
    }

    private static IPropertySymbol? GetPrimaryKeyProperty(ITypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Name == "Id" || prop.Name == type.Name + "Id")
                return prop;
        }
        return null;
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
