#nullable enable
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace AuditLog.Generator;

[Generator]
public sealed class AuditLogGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configurators = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => ConfiguratorDetector.IsCandidate(node),
                transform: static (ctx, _) => ConfiguratorDetector.GetSemanticTarget(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        context.RegisterSourceOutput(configurators, static (ctx, configs) => GenerateAll(ctx, configs));
    }

    private static readonly DiagnosticDescriptor ParseErrorDescriptor = new(
        id: "ALG001",
        title: "AuditLog configuration parse error",
        messageFormat: "{0}",
        category: "AuditLog.Generator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static void GenerateAll(SourceProductionContext context, ImmutableArray<ConfiguratorInfo> configurators)
    {
        if (configurators.Length == 0) return;

        foreach (var config in configurators)
        {
            foreach (var error in config.ParseErrors)
                context.ReportDiagnostic(Diagnostic.Create(ParseErrorDescriptor, Location.None, error));

            RootEntityGenerator.GenerateAuditLogClass(context, config);
            RootEntityGenerator.GenerateEntityMapClass(context, config);
            RootEntityGenerator.GenerateDescriptorClass(context, config);

            foreach (var col in config.Collections)
            {
                CollectionEntityGenerator.GenerateAuditLogClass(context, config, col);
                CollectionEntityGenerator.GenerateEntityMapClass(context, config, col);
                CollectionEntityGenerator.GenerateDescriptorClass(context, config, col);
            }
        }

        ExtensionGenerator.GenerateRegistryExtension(context, configurators);
        ExtensionGenerator.GenerateModelBuilderExtension(context, configurators);
    }
}
