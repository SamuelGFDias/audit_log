#nullable enable
using System.Collections.Immutable;
using System.Text;

namespace AuditLog.Generator;

internal static class Helpers
{
    public static string GetNs(ConfiguratorInfo config)
    {
        var ns = config.ConfiguratorNamespace;
        if (string.IsNullOrEmpty(ns) || ns.StartsWith("<"))
            return "AuditLog.Generated";
        return ns;
    }

    public static void BuildProperties(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string entityName)
    {
        foreach (var prop in properties)
        {
            if (prop.IsIgnored || prop.IsKey) continue;
            var nullable = prop.IsRequired ? "" : "?";
            sb.AppendLine($"        public string{nullable} {prop.PropertyName} {{ get; set; }}");
            sb.AppendLine();
        }
    }

    public static void BuildCollectionProperties(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string parentKey, string childKey)
    {
        foreach (var prop in properties)
        {
            if (prop.PropertyName == parentKey || prop.PropertyName == childKey) continue;
            sb.AppendLine($"        public string? {prop.PropertyName} {{ get; set; }}");
            sb.AppendLine();
        }
    }

    public static void BuildEntityMapProperties(StringBuilder sb, ImmutableArray<PropertyConfig> properties)
    {
        BuildEntityMapProperties(sb, properties, null, null);
    }

    public static void BuildEntityMapProperties(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string? exclude1, string? exclude2)
    {
        foreach (var prop in properties)
        {
            if (prop.IsIgnored || prop.IsKey) continue;
            if (prop.PropertyName == exclude1 || prop.PropertyName == exclude2) continue;
            sb.AppendLine($"            builder.Property(x => x.{prop.PropertyName})");

            if (prop.MaxLength > 0)
                sb.AppendLine($"                .HasMaxLength({prop.MaxLength})");

            if (prop.IsRequired)
                sb.AppendLine($"                .IsRequired()");

            if (prop.ColumnName is not null)
                sb.AppendLine($"                .HasColumnName(\"{prop.ColumnName}\")");

            sb.AppendLine("                ;");
            sb.AppendLine();
        }
    }

    public static void BuildDescriptorAssignments(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string indent)
    {
        BuildDescriptorAssignments(sb, properties, indent, null, null);
    }

    public static void BuildDescriptorAssignments(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string indent, string? exclude1, string? exclude2)
    {
        foreach (var prop in properties)
        {
            if (prop.IsIgnored || prop.IsKey) continue;
            if (prop.PropertyName == exclude1 || prop.PropertyName == exclude2) continue;
            if (prop.IsSensitive)
                sb.AppendLine($"{indent}{prop.PropertyName} = \"***\",");
            else
                sb.AppendLine($"{indent}{prop.PropertyName} = entity.{prop.PropertyName}!.ToString()!,");
        }
    }

    public static void BuildChangedPropertiesCheck(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string entityName)
    {
        BuildChangedPropertiesCheck(sb, properties, entityName, null, null);
    }

    public static void BuildChangedPropertiesCheck(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string entityName, string? exclude1, string? exclude2)
    {
        foreach (var prop in properties)
        {
            if (prop.IsIgnored || prop.IsKey) continue;
            if (prop.PropertyName == exclude1 || prop.PropertyName == exclude2) continue;
            sb.AppendLine($"            if (entry.Property(x => x.{prop.PropertyName}).IsModified)");
            sb.AppendLine($"                changed.Add(nameof({entityName}.{prop.PropertyName}));");
        }
    }
}
