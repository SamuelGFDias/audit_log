#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AuditLog.Generator.SoftDelete;

internal sealed class RelationshipConfig
{
    public string PrincipalEntity { get; set; } = "";
    public string NavigationProperty { get; set; } = "";
    public string DependentEntityFullName { get; set; } = "";
    public string DependentEntityName { get; set; } = "";
    public string DependentPkName { get; set; } = "Id";
    public string FkPropertyName { get; set; } = "";
    public string FkPropertyType { get; set; } = "global::System.Guid";
    public bool FkIsNullable { get; set; }
    public string PrincipalKeyName { get; set; } = "Id";
    public string DeleteBehavior { get; set; } = "Cascade";
    public bool IsOwnership { get; set; }
    public bool DependentIsSoftDelete { get; set; }
}

internal static class FluentApiParser
{
    public static ImmutableArray<RelationshipConfig> ParseOnModelCreating(
        ClassDeclarationSyntax classDecl,
        INamedTypeSymbol dbContextSymbol,
        ImmutableArray<EntityInfo> entities,
        GeneratorSyntaxContext context)
    {
        var onModelCreating = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "OnModelCreating");

        if (onModelCreating?.Body is null)
            return ImmutableArray<RelationshipConfig>.Empty;

        var entitySymbols = entities.ToDictionary(e => e.Name, e => e);
        var compilation = context.SemanticModel.Compilation;
        var configs = new List<RelationshipConfig>();

        foreach (var statement in onModelCreating.Body.Statements)
        {
            if (statement is not ExpressionStatementSyntax exprStmt) continue;
            if (exprStmt.Expression is not InvocationExpressionSyntax invocation) continue;

            if (invocation.Expression is MemberAccessExpressionSyntax ma)
            {
                if (ma.Name is GenericNameSyntax { Identifier: { Text: "Entity" } } genericName)
                {
                    ParseEntityInline(invocation, genericName, entities, entitySymbols, configs);
                }
                else if (ma.Name.Identifier.Text == "ApplyConfiguration")
                {
                    ParseApplyConfiguration(invocation, compilation, entities, entitySymbols, configs);
                }
                else if (ma.Name.Identifier.Text == "ApplyConfigurationsFromAssembly")
                {
                    ParseApplyConfigurationsFromAssembly(compilation, entities, entitySymbols, configs);
                }
            }
        }

        return configs.ToImmutableArray();
    }

    private static void ParseEntityInline(
        InvocationExpressionSyntax invocation,
        GenericNameSyntax genericName,
        ImmutableArray<EntityInfo> entities,
        Dictionary<string, EntityInfo> entitySymbols,
        List<RelationshipConfig> configs)
    {
        var entityName = genericName.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();
        if (entityName is null) return;

        var configLambda = invocation.ArgumentList.Arguments
            .FirstOrDefault()?.Expression as LambdaExpressionSyntax;

        if (configLambda?.Body is BlockSyntax block)
        {
            foreach (var innerStmt in block.Statements)
            {
                if (innerStmt is not ExpressionStatementSyntax innerExpr) continue;
                var configsFromChain = ParseRelationshipChain(
                    innerExpr.Expression, entityName, entities, entitySymbols);
                configs.AddRange(configsFromChain);
            }
        }
    }

    private static void ParseApplyConfiguration(
        InvocationExpressionSyntax invocation,
        Compilation compilation,
        ImmutableArray<EntityInfo> entities,
        Dictionary<string, EntityInfo> entitySymbols,
        List<RelationshipConfig> configs)
    {
        var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (arg is not ObjectCreationExpressionSyntax creation) return;

        var mapTypeName = creation.Type.ToString();
        var mapClass = FindClassDeclaration(compilation, mapTypeName);
        if (mapClass is null) return;

        var tree = mapClass.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(tree);
        var entityTypeSymbol = ExtractEntityTypeFromSemanticModel(mapClass, semanticModel);
        var entityName = entityTypeSymbol?.Name;
        if (entityName is null)
        {
            entityName = ResolveEntityFromMapName(mapTypeName);
            if (entityName is null) return;
        }

        ParseEntityMapConfigure(mapClass, entityName, compilation, entities, entitySymbols, configs);
    }

    private static void ParseApplyConfigurationsFromAssembly(
        Compilation compilation,
        ImmutableArray<EntityInfo> entities,
        Dictionary<string, EntityInfo> entitySymbols,
        List<RelationshipConfig> configs)
    {
        var seenEntity = new HashSet<string>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            var semanticModel = compilation.GetSemanticModel(tree);

            foreach (var cls in classes)
            {
                var entityTypeArg = ExtractEntityTypeFromSemanticModel(cls, semanticModel);
                if (entityTypeArg is null) continue;
                if (!seenEntity.Add(entityTypeArg.Name)) continue;

                var entityName = entityTypeArg.Name.ToString();
                ParseEntityMapConfigure(cls, entityName, compilation, entities, entitySymbols, configs);
            }
        }
    }

    internal static ITypeSymbol? ExtractEntityTypeFromSemanticModel(
        ClassDeclarationSyntax cls, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(cls) as INamedTypeSymbol;
        if (symbol is null) return null;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface is INamedTypeSymbol
                {
                    Name: "IEntityTypeConfiguration",
                    IsGenericType: true
                } named &&
                named.TypeArguments.Length == 1)
            {
                return named.TypeArguments[0];
            }
        }

        return null;
    }

    private static void ParseEntityMapConfigure(
        ClassDeclarationSyntax mapClass,
        string entityName,
        Compilation compilation,
        ImmutableArray<EntityInfo> entities,
        Dictionary<string, EntityInfo> entitySymbols,
        List<RelationshipConfig> configs)
    {
        var configureMethod = mapClass.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Configure");

        if (configureMethod?.Body is null) return;

        foreach (var stmt in configureMethod.Body.Statements)
        {
            if (stmt is not ExpressionStatementSyntax exprStmt) continue;
            var configsFromChain = ParseRelationshipChain(
                exprStmt.Expression, entityName, entities, entitySymbols);
            configs.AddRange(configsFromChain);
        }
    }

    private static string? ResolveEntityFromMapName(string mapTypeName)
    {
        var name = mapTypeName;
        var suffixes = new[] { "EntityMap", "EntityConfiguration", "Map", "Configuration" };
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix))
                return name.Substring(0, name.Length - suffix.Length);
        }
        return name;
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

    private static List<RelationshipConfig> ParseRelationshipChain(
        ExpressionSyntax expression,
        string principalEntity,
        ImmutableArray<EntityInfo> entities,
        Dictionary<string, EntityInfo> entitySymbols)
    {
        var configs = new List<RelationshipConfig>();

        if (expression is not InvocationExpressionSyntax invocation) return configs;

        var chain = WalkChain(invocation);

        var hasOneOrMany = chain.FirstOrDefault(c => c.Method is "HasOne" or "HasMany");
        if (hasOneOrMany is null) return configs;

        var navProperty = ExtractPropertyFromLambda(hasOneOrMany.Arg);
        var hasOne = hasOneOrMany.Method == "HasOne";

        var withOneOrMany = chain.FirstOrDefault(c => c.Method is "WithOne" or "WithMany");
        var fkCall = chain.FirstOrDefault(c => c.Method == "HasForeignKey");
        var ownsCall = chain.FirstOrDefault(c => c.Method is "OwnsOne" or "OwnsMany");

        if (ownsCall is not null)
        {
            configs.Add(new RelationshipConfig
            {
                PrincipalEntity = principalEntity,
                NavigationProperty = navProperty ?? "",
                IsOwnership = true
            });
            return configs;
        }

        var rawFkName = fkCall is not null
            ? ExtractPropertyFromLambda(fkCall.Arg) ?? principalEntity + "Id"
            : principalEntity + "Id";

        // For HasOne, the FK is on the current entity (which is the dependent).
        // The principal is the navigation target.
        // For HasMany.WithOne, the FK is on the WithOne entity (the dependent).
        // The principal is the current entity.
        if (hasOne)
        {
            // HasOne<T>() without lambda: use generic type argument
            var hasOneGeneric = string.IsNullOrEmpty(navProperty) && !string.IsNullOrEmpty(hasOneOrMany.GenericTypeArg)
                ? hasOneOrMany.GenericTypeArg
                : null;

            string resolvedPrincipal;
            if (hasOneGeneric is not null)
            {
                resolvedPrincipal = hasOneGeneric;
            }
            else
            {
                resolvedPrincipal = ResolveDependentEntityName(
                    principalEntity, navProperty ?? "", rawFkName, entities, entitySymbols);
            }

            if (string.IsNullOrEmpty(resolvedPrincipal) || resolvedPrincipal == principalEntity)
                return configs;

            var hFkNullable = false;
            var hFkType = "global::System.Guid";
            if (entitySymbols.TryGetValue(principalEntity, out var hDepInfo))
            {
                hFkNullable = hDepInfo.PrimaryKeyType.Contains("?");
                hFkType = hDepInfo.PrimaryKeyType;
            }

            var hBehavior = OnDeleteBehavior(chain);
            var hDepIsSoftDelete = entitySymbols.TryGetValue(principalEntity, out var hDepSym) && hDepSym.IsSoftDelete;
            var hDepFullName = entitySymbols.TryGetValue(principalEntity, out var hDep)
                ? hDep.FullName
                : principalEntity;
            var hPkName = entitySymbols.TryGetValue(resolvedPrincipal, out var hPrincipalSym)
                ? hPrincipalSym.PrimaryKeyName
                : "Id";

            var hDepName = principalEntity;
            var hDepPkName = entitySymbols.TryGetValue(hDepName, out var hDepSymPk)
                ? hDepSymPk.PrimaryKeyName
                : "Id";

            configs.Add(new RelationshipConfig
            {
                PrincipalEntity = resolvedPrincipal,
                NavigationProperty = navProperty ?? "",
                DependentEntityFullName = hDepFullName,
                DependentEntityName = hDepName,
                DependentPkName = hDepPkName,
                FkPropertyName = rawFkName,
                FkPropertyType = hFkType,
                FkIsNullable = hFkNullable,
                PrincipalKeyName = hPkName,
                DeleteBehavior = hBehavior,
                IsOwnership = false,
                DependentIsSoftDelete = hDepIsSoftDelete
            });

            return configs;
        }

        var fkName = rawFkName;
        var dependentEntityName = ResolveDependentEntityName(
            principalEntity, navProperty ?? "", fkName, entities, entitySymbols);

        var fkNullable = false;
        var fkType = "global::System.Guid";
        if (!string.IsNullOrEmpty(dependentEntityName) && entitySymbols.TryGetValue(dependentEntityName, out var depInfo))
        {
            fkNullable = depInfo.PrimaryKeyType.Contains("?");
            fkType = depInfo.PrimaryKeyType;
        }

        var behavior = OnDeleteBehavior(chain);

        var dependentIsSoftDelete = entitySymbols.TryGetValue(dependentEntityName, out var depSym) && depSym.IsSoftDelete;

        var dependentFullName = "";
        if (!string.IsNullOrEmpty(dependentEntityName))
        {
            dependentFullName = entitySymbols.TryGetValue(dependentEntityName, out var dep)
                ? dep.FullName
                : dependentEntityName;
        }

        var pkName = entitySymbols.TryGetValue(principalEntity, out var principalSym)
            ? principalSym.PrimaryKeyName
            : "Id";

        var depPkName = entitySymbols.TryGetValue(dependentEntityName, out var depSymPk)
            ? depSymPk.PrimaryKeyName
            : "Id";

        configs.Add(new RelationshipConfig
        {
            PrincipalEntity = principalEntity,
            NavigationProperty = navProperty ?? "",
            DependentEntityFullName = dependentFullName,
            DependentEntityName = dependentEntityName,
            DependentPkName = depPkName,
            FkPropertyName = fkName,
            FkPropertyType = fkType,
            FkIsNullable = fkNullable,
            PrincipalKeyName = pkName,
            DeleteBehavior = behavior,
            IsOwnership = false,
            DependentIsSoftDelete = dependentIsSoftDelete
        });

        return configs;
    }

    private static string OnDeleteBehavior(List<MethodCallInfo> chain)
    {
        var onDelete = chain.FirstOrDefault(c => c.Method == "OnDelete");
        if (onDelete is null) return "Cascade";

        var arg = onDelete.Arg;
        if (arg is null) return "Cascade";

        if (arg is MemberAccessExpressionSyntax ma)
            return ma.Name.Identifier.Text;

        if (arg is IdentifierNameSyntax ins)
        {
            return ins.Identifier.Text switch
            {
                "Cascade" or "Restrict" or "SetNull" or "ClientCascade" or "ClientSetNull" or "NoAction" => ins.Identifier.Text,
                _ => "Cascade"
            };
        }

        return "Cascade";
    }

    private static string ResolveDependentEntityName(
        string principalEntity, string navProperty, string fkName,
        ImmutableArray<EntityInfo> entities, Dictionary<string, EntityInfo> entitySymbols)
    {
        if (entities.Length == 0) return "";
        var candidates = entities.Where(e => e.Name != principalEntity).ToList();
        if (candidates.Count == 0) return "";
        if (candidates.Count == 1) return candidates[0].Name;

        var navSingular = navProperty.EndsWith("s") ? navProperty.Substring(0, navProperty.Length - 1) : navProperty;

        var byNav = candidates.FirstOrDefault(e => e.Name == navProperty);
        if (byNav is not null) return byNav.Name;

        var bySingular = candidates.FirstOrDefault(e => e.Name == navSingular);
        if (bySingular is not null) return bySingular.Name;

        var byContains = candidates.FirstOrDefault(e =>
        {
            if (e.Name.IndexOf(navSingular, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (navSingular.IndexOf(e.Name, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var navWords = SplitPascalCase(navSingular);
            var entWords = SplitPascalCase(e.Name);
            return navWords.Any(nw => entWords.Any(ew =>
                string.Equals(nw, ew, System.StringComparison.OrdinalIgnoreCase)));
        });
        if (byContains is not null) return byContains.Name;

        var fkTarget = fkName.EndsWith("Id") ? fkName.Substring(0, fkName.Length - 2) : "";
        if (!string.IsNullOrEmpty(fkTarget) && fkTarget != principalEntity)
        {
            var byFK = candidates.FirstOrDefault(e => e.Name == fkTarget);
            if (byFK is not null) return byFK.Name;
        }

        return candidates[0].Name;
    }

    private static List<string> SplitPascalCase(string value)
    {
        var words = new List<string>();
        var start = 0;
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            {
                words.Add(value.Substring(start, i - start));
                start = i;
            }
        }
        if (start < value.Length)
            words.Add(value.Substring(start));
        return words;
    }

    private static string? ExtractPropertyFromLambda(ExpressionSyntax? expression)
    {
        if (expression is SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax ma })
            return ma.Name.Identifier.Text;
        if (expression is ParenthesizedLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax ma2 })
            return ma2.Name.Identifier.Text;
        return null;
    }

    private static List<MethodCallInfo> WalkChain(InvocationExpressionSyntax invocation)
    {
        var calls = new List<MethodCallInfo>();
        var current = invocation;

        while (true)
        {
            var methodName = current.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => null
            };

            if (methodName is null) break;

            var firstArg = current.ArgumentList.Arguments.Count > 0
                ? current.ArgumentList.Arguments[0].Expression
                : null;

            var genericTypeArg = current.Expression is MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax generic
                } && generic.TypeArgumentList.Arguments.Count == 1
                ? generic.TypeArgumentList.Arguments[0].ToString()
                : null;

            calls.Add(new MethodCallInfo(methodName, firstArg, genericTypeArg));

            if (current.Expression is MemberAccessExpressionSyntax { Expression: InvocationExpressionSyntax inner })
            {
                current = inner;
            }
            else
            {
                break;
            }
        }

        calls.Reverse();
        return calls;
    }

    private sealed class MethodCallInfo
    {
        public string Method { get; }
        public ExpressionSyntax? Arg { get; }
        public string? GenericTypeArg { get; }

        public MethodCallInfo(string method, ExpressionSyntax? arg, string? genericTypeArg = null)
        {
            Method = method;
            Arg = arg;
            GenericTypeArg = genericTypeArg;
        }
    }
}
