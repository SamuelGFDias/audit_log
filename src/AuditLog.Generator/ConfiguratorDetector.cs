#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace AuditLog.Generator;

internal static class ConfiguratorDetector
{
    private const string GenerateAuditLogAttribute = "AuditLog.Abstractions.GenerateAuditLogAttribute";

    public static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0, BaseList: not null };
    }

    public static ConfiguratorInfo? GetSemanticTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (typeSymbol is null) return null;

        if (!typeSymbol.HasAttribute(GenerateAuditLogAttribute)) return null;

        var baseType = typeSymbol.BaseType;
        if (baseType is null) return null;
        if (baseType.OriginalDefinition.Name != "AuditConfigurator") return null;
        if (baseType.OriginalDefinition.ContainingNamespace?.ToDisplayString() != "AuditLog.Abstractions") return null;

        var entityType = baseType.TypeArguments[0];
        var entityName = entityType.Name;
        var entityNamespace = entityType.ContainingNamespace?.ToDisplayString() ?? "";

        if (entityNamespace.StartsWith("<"))
            entityNamespace = "";
        
        var configuratorNamespace = typeSymbol.ContainingNamespace.ToDisplayString();
        var configuratorName = typeSymbol.Name;
        var auditLogName = entityName + "AuditLog";

        var entityProperties = new List<PropertyConfig>();
        var collectionConfigs = new List<CollectionConfig>();

        var constructor = classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor?.Body is not null)
        {
            foreach (var statement in constructor.Body.Statements)
            {
                if (statement is ExpressionStatementSyntax exprStmt)
                {
                    ExpressionParser.ParseStatementExpression(
                        exprStmt.Expression, entityProperties, collectionConfigs, entityType, entityName);
                }
            }
        }

        var configuredNames = new HashSet<string>();
        foreach (var p in entityProperties)
            configuredNames.Add(p.PropertyName);

        var pkName = DetectPrimaryKeyName(entityType);
        var pkType = ResolveKeyType(entityType, pkName);

        foreach (var member in entityType.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.IsStatic || prop.IsIndexer) continue;
            if (configuredNames.Contains(prop.Name)) continue;
            if (RoslynExtensions.GetCollectionElementType(prop.Type) is not null) continue;
            if (!IsSimpleScalarType(prop.Type)) continue;

            var isKey = prop.Name == pkName;
            entityProperties.Add(new PropertyConfig(
                prop.Name, null, isKey, false, false, false, null, 0, false));
        }

        return new ConfiguratorInfo(
            entityNamespace,
            configuratorNamespace,
            configuratorName,
            entityName, 
            auditLogName,
            pkType.fullName, pkType.simpleName,
            [..entityProperties],
            [..collectionConfigs]);
    }

    private static bool IsSimpleScalarType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None) return true;
        if (type.TypeKind == TypeKind.Enum) return true;
        if (type is INamedTypeSymbol { IsGenericType: true, Name: "Nullable" }) return true;

        var name = type.ToDisplayString();
        return name is "System.Guid" or "System.DateTimeOffset"
            or "System.DateOnly" or "System.TimeOnly"
            or "System.Half" or "System.Int128" or "System.UInt128"
            or "System.Numerics.BigInteger";
    }

    private static string? DetectPrimaryKeyName(ITypeSymbol entityType)
    {
        var entityName = entityType.Name;
        foreach (var member in entityType.GetMembers())
        {
            if (member is not IPropertySymbol prop) continue;
            if (prop.Name == "Id" || prop.Name == entityName + "Id")
                return prop.Name;
        }

        return null;
    }

    private static (string fullName, string simpleName) ResolveKeyType(ITypeSymbol entityType, string? pkName)
    {
        if (pkName is not null)
        {
            foreach (var member in entityType.GetMembers())
            {
                if (member is IPropertySymbol prop && prop.Name == pkName)
                {
                    var typeName = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::System.Int32", "int")
                        .Replace("global::System.Int64", "long")
                        .Replace("global::System.String", "string")
                        .Replace("global::System.Boolean", "bool")
                        .Replace("global::System.Byte", "byte")
                        .Replace("global::System.Single", "float")
                        .Replace("global::System.Double", "double")
                        .Replace("global::System.Decimal", "decimal")
                        .Replace("global::System.Char", "char")
                        .Replace("global::System.Object", "object");
                    var simpleName = prop.Type.Name;
                    return (typeName, simpleName);
                }
            }
        }

        return ("global::System.Guid", "Guid");
    }
}