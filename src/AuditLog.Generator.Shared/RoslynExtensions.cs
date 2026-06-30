#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace AuditLog.Generator;

internal static class RoslynExtensions
{
    public static string? GetFullName(this ITypeSymbol type)
        => type is INamedTypeSymbol named
            ? named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;

    public static bool HasAttribute(this ISymbol symbol, string attributeFullName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == attributeFullName)
                return true;
        }
        return false;
    }

    public static bool ImplementsInterface(this ITypeSymbol type, string interfaceFullName)
    {
        if (type.ToDisplayString() == interfaceFullName)
            return true;

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == interfaceFullName)
                return true;
        }
        return false;
    }

    public static string? ExtractPropertyNameFromLambda(ExpressionSyntax? expression)
    {
        if (expression is SimpleLambdaExpressionSyntax simple)
            return ExtractFromLambdaBody(simple.Body);

        if (expression is ParenthesizedLambdaExpressionSyntax parenthesized)
            return ExtractFromLambdaBody(parenthesized.Body);

        return null;
    }

    private static string? ExtractFromLambdaBody(CSharpSyntaxNode body)
    {
        if (body is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Name.Identifier.Text;

        if (body is InvocationExpressionSyntax inv &&
            inv.Expression is MemberAccessExpressionSyntax invMember)
            return invMember.Name.Identifier.Text;

        return null;
    }

    public static ITypeSymbol? GetCollectionElementType(ITypeSymbol propertyType)
    {
        if (propertyType is INamedTypeSymbol named)
        {
            foreach (var iface in named.AllInterfaces)
            {
                if (iface is INamedTypeSymbol { Name: "IEnumerable" } enumerable &&
                    enumerable.TypeArguments.Length == 1)
                    return enumerable.TypeArguments[0];
            }

            if (named.TypeArguments.Length == 1 &&
                named.OriginalDefinition.SpecialType == SpecialType.None)
                return named.TypeArguments[0];
        }
        return null;
    }
}
