#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AuditLog.Generator;

internal static class ConfiguratorDetector
{
    public const string GenerateAuditLogAttribute = "AuditLog.Abstractions.GenerateAuditLogAttribute";

    public static bool IsCandidate(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax cds &&
               cds.AttributeLists.Count > 0 &&
               cds.BaseList is not null;
    }

    public static ConfiguratorInfo? GetSemanticTarget(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (typeSymbol is null) return null;

        var hasAttr = false;
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == GenerateAuditLogAttribute)
            { hasAttr = true; break; }
        }
        if (!hasAttr) return null;

        var baseType = typeSymbol.BaseType;
        if (baseType is null) return null;
        if (baseType.OriginalDefinition.Name != "AuditConfigurator") return null;
        if (baseType.OriginalDefinition.ContainingNamespace?.ToDisplayString() != "AuditLog.Abstractions") return null;

        var entityType = baseType.TypeArguments[0];
        var entityName = entityType.Name;
        var entityNamespace = entityType.ContainingNamespace.ToDisplayString();
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

        return new ConfiguratorInfo(
            entityNamespace, configuratorNamespace, configuratorName,
            entityName, auditLogName,
            entityProperties.ToImmutableArray(),
            collectionConfigs.ToImmutableArray());
    }
}
