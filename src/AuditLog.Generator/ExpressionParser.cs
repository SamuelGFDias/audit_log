#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AuditLog.Generator;

internal static class ExpressionParser
{
    public static void ParseStatementExpression(
        ExpressionSyntax expression,
        List<PropertyConfig> entityProperties,
        List<CollectionConfig> collectionConfigs,
        ITypeSymbol entityType,
        string entityName)
    {
        var innermost = FindInnermostInvocation(expression);
        if (innermost is null) return;

        var methodName = GetMethodName(innermost);
        if (methodName == "For")
            ParseForChain(innermost, expression, entityProperties);
        else if (methodName == "ForEach")
            ParseForEachChain(innermost, expression, entityType, entityName, collectionConfigs);
        else if (methodName == "ForOwned")
            ParseForOwnedChain(innermost, expression, entityType, entityProperties);
    }

    private static InvocationExpressionSyntax? FindInnermostInvocation(ExpressionSyntax expression)
    {
        while (expression is InvocationExpressionSyntax inv)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Expression is InvocationExpressionSyntax inner)
                expression = inner;
            else
                return inv;
        }
        return null;
    }

    private static void ParseForChain(
        InvocationExpressionSyntax forCall,
        ExpressionSyntax fullExpression,
        List<PropertyConfig> configs)
    {
        var propertyName = ExtractPropertyName(forCall);
        if (propertyName is null) return;

        var isKey = false;
        var isIgnored = false;
        var isSensitive = false;
        var alwaysAudit = false;
        string? columnName = null;
        var maxLength = 0;
        var isRequired = false;

        CollectModifiers(fullExpression, ref isKey, ref isIgnored, ref isSensitive,
            ref alwaysAudit, ref columnName, ref maxLength, ref isRequired);

        configs.Add(new PropertyConfig(
            propertyName, null, isKey, isIgnored, isSensitive,
            alwaysAudit, columnName, maxLength, isRequired));
    }

    private static void CollectModifiers(
        ExpressionSyntax expression,
        ref bool isKey, ref bool isIgnored, ref bool isSensitive, ref bool alwaysAudit,
        ref string? columnName, ref int maxLength, ref bool isRequired)
    {
        if (expression is not InvocationExpressionSyntax invocation) return;

        switch (GetMethodName(invocation))
        {
            case "Key": isKey = true; break;
            case "Ignore": isIgnored = true; break;
            case "Sensitive": isSensitive = true; break;
            case "AlwaysAudit": alwaysAudit = true; break;
            case "WithColumnName":
                if (GetFirstArg(invocation) is LiteralExpressionSyntax lit)
                    columnName = lit.Token.ValueText;
                break;
            case "HasMaxLength":
                if (GetFirstArg(invocation) is LiteralExpressionSyntax num && num.IsKind(SyntaxKind.NumericLiteralExpression))
                    maxLength = (int)num.Token.Value!;
                break;
            case "IsRequired": isRequired = true; break;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax ma &&
            ma.Expression is ExpressionSyntax inner)
            CollectModifiers(inner, ref isKey, ref isIgnored, ref isSensitive,
                ref alwaysAudit, ref columnName, ref maxLength, ref isRequired);
    }

    private static void ParseForEachChain(
        InvocationExpressionSyntax forEachCall,
        ExpressionSyntax fullExpression,
        ITypeSymbol entityType,
        string entityName,
        List<CollectionConfig> configs)
    {
        var propertyName = ExtractPropertyName(forEachCall);
        if (propertyName is null) return;

        var elementName = ResolveCollectionElementType(entityType, propertyName);
        var elementType = ResolveCollectionElementSymbol(entityType, propertyName);
        var auditLogName = elementName + "AuditLog";
        string? parentKey = null;
        string? childKey = null;
        var itemConfigs = new List<PropertyConfig>();

        CollectForEachModifiers(fullExpression, ref parentKey, ref childKey, itemConfigs);

        var resolvedParentKey = parentKey ?? elementName + "Id";
        var resolvedChildKey = childKey ?? "Id";

        var parentKeyType = ResolvePropertyTypeString(elementType, resolvedParentKey);
        var childKeyType = ResolvePropertyTypeString(elementType, resolvedChildKey);

        configs.Add(new CollectionConfig(
            elementName, auditLogName,
            resolvedParentKey, resolvedChildKey,
            parentKeyType, childKeyType,
            itemConfigs.ToImmutableArray()));
    }

    private static void ParseForOwnedChain(
        InvocationExpressionSyntax forOwnedCall,
        ExpressionSyntax fullExpression,
        ITypeSymbol entityType,
        List<PropertyConfig> configs)
    {
        if (forOwnedCall.ArgumentList.Arguments.Count < 2)
            return;

        var navArg = forOwnedCall.ArgumentList.Arguments[0].Expression;
        var configArg = forOwnedCall.ArgumentList.Arguments[1].Expression;

        ExpressionSyntax? navBody = null;
        if (navArg is SimpleLambdaExpressionSyntax sl)
            navBody = sl.Body as ExpressionSyntax;
        else if (navArg is ParenthesizedLambdaExpressionSyntax pl)
            navBody = pl.Body as ExpressionSyntax;

        if (navBody is not MemberAccessExpressionSyntax navMa)
            return;

        var navigation = navMa.Name.Identifier.Text;

        if (configArg is not LambdaExpressionSyntax configLambda)
            return;

        if (configLambda.Body is not BlockSyntax block)
            return;

        foreach (var stmt in block.Statements)
        {
            if (stmt is not ExpressionStatementSyntax exprStmt)
                continue;

            var innermost = FindInnermostInvocation(exprStmt.Expression);
            if (innermost is null) continue;

            var methodName = GetMethodName(innermost);
            if (methodName != "For")
                continue;

            var propertyName = ExtractPropertyName(innermost);
            if (propertyName is null) continue;

            var isKey = false;
            var isIgnored = false;
            var isSensitive = false;
            var alwaysAudit = false;
            string? columnName = null;
            var maxLength = 0;
            var isRequired = false;

            CollectModifiers(exprStmt.Expression, ref isKey, ref isIgnored, ref isSensitive,
                ref alwaysAudit, ref columnName, ref maxLength, ref isRequired);

            configs.Add(new PropertyConfig(
                propertyName, navigation, isKey, isIgnored, isSensitive,
                alwaysAudit, columnName, maxLength, isRequired));
        }
    }

    private static void CollectForEachModifiers(
        ExpressionSyntax expression,
        ref string? parentKey, ref string? childKey,
        List<PropertyConfig> itemConfigs)
    {
        if (expression is not InvocationExpressionSyntax invocation) return;

        var methodName = GetMethodName(invocation);
        var arg = GetFirstArg(invocation);

        switch (methodName)
        {
            case "ParentKey" when arg is LambdaExpressionSyntax pl:
                parentKey = ExtractPropertyFromLambda(pl); break;
            case "Key" when arg is LambdaExpressionSyntax kl:
                childKey = ExtractPropertyFromLambda(kl); break;
            case "Configure" when arg is LambdaExpressionSyntax cl:
                ParseConfigureLambda(cl, itemConfigs); break;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax ma &&
            ma.Expression is ExpressionSyntax inner)
            CollectForEachModifiers(inner, ref parentKey, ref childKey, itemConfigs);
    }

    public static void ParseConfigureLambda(
        LambdaExpressionSyntax lambda,
        List<PropertyConfig> configs)
    {
        if (lambda.Body is not BlockSyntax block) return;

        foreach (var stmt in block.Statements)
        {
            if (stmt is ExpressionStatementSyntax exprStmt &&
                exprStmt.Expression is InvocationExpressionSyntax invocation)
            {
                var methodName = GetMethodName(invocation);
                if (methodName == "For")
                {
                    var propertyName = ExtractPropertyName(invocation);
                    if (propertyName is not null)
                        configs.Add(new PropertyConfig(
                            propertyName, null, false, false, false, false, null, 0, false));
                }
            }
        }
    }

    public static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            IdentifierNameSyntax ins => ins.Identifier.Text,
            GenericNameSyntax gns => gns.Identifier.Text,
            _ => null
        };
    }

    public static ExpressionSyntax? GetFirstArg(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments.Count > 0
            ? invocation.ArgumentList.Arguments[0].Expression
            : null;
    }

    public static string? ExtractPropertyName(InvocationExpressionSyntax invocation)
    {
        var arg = GetFirstArg(invocation);
        if (arg is null) return null;

        ExpressionSyntax? body = null;
        if (arg is SimpleLambdaExpressionSyntax sl)
            body = sl.Body as ExpressionSyntax;
        else if (arg is ParenthesizedLambdaExpressionSyntax pl)
            body = pl.Body as ExpressionSyntax;

        if (body is MemberAccessExpressionSyntax ma)
            return ma.Name.Identifier.Text;

        return null;
    }

    private static string? ExtractPropertyFromLambda(LambdaExpressionSyntax lambda)
    {
        if (lambda.Body is MemberAccessExpressionSyntax ma)
            return ma.Name.Identifier.Text;
        return null;
    }

    private static string ResolveCollectionElementType(ITypeSymbol entityType, string propertyName)
    {
        foreach (var member in entityType.GetMembers())
        {
            if (member.Name != propertyName) continue;

            INamedTypeSymbol? named = member switch
            {
                IPropertySymbol p => p.Type as INamedTypeSymbol,
                IFieldSymbol f => f.Type as INamedTypeSymbol,
                _ => null
            };

            if (named is not null && named.IsGenericType && named.TypeArguments.Length > 0)
                return named.TypeArguments[0].Name;

            return propertyName;
        }

        return propertyName;
    }

    private static ITypeSymbol? ResolveCollectionElementSymbol(ITypeSymbol entityType, string propertyName)
    {
        foreach (var member in entityType.GetMembers())
        {
            if (member.Name != propertyName) continue;

            INamedTypeSymbol? named = member switch
            {
                IPropertySymbol p => p.Type as INamedTypeSymbol,
                IFieldSymbol f => f.Type as INamedTypeSymbol,
                _ => null
            };

            if (named is not null && named.IsGenericType && named.TypeArguments.Length > 0)
                return named.TypeArguments[0];
        }

        return null;
    }

    private static string ResolvePropertyTypeString(ITypeSymbol? type, string propertyName)
    {
        if (type is null) return "global::System.Guid";

        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol prop && prop.Name == propertyName)
            {
                var typeStr = prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
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
                return typeStr;
            }
        }

        return "global::System.Guid";
    }
}
