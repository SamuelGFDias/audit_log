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
            var init = prop.IsRequired ? " = null!;" : "";
            sb.AppendLine($"        public string{nullable} {prop.FullPropertyName} {{ get; set; }}{init}");
            sb.AppendLine();
        }
    }

    public static void BuildCollectionProperties(StringBuilder sb, ImmutableArray<PropertyConfig> properties, string parentKey, string childKey)
    {
        foreach (var prop in properties)
        {
            if (prop.PropertyName == parentKey || prop.PropertyName == childKey) continue;
            sb.AppendLine($"        public string? {prop.FullPropertyName} {{ get; set; }}");
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
            sb.AppendLine($"            builder.Property(x => x.{prop.FullPropertyName})");

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

            var valueExpr = prop.NavigationPrefix is null
                ? $"entity.{prop.PropertyName}"
                : $"entity.{prop.NavigationPrefix}.{prop.PropertyName}";

            if (prop.IsSensitive)
                sb.AppendLine($"{indent}{prop.FullPropertyName} = \"***\",");
            else
                sb.AppendLine($"{indent}{prop.FullPropertyName} = global::System.Convert.ToString({valueExpr}) ?? string.Empty,");
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

            var propertyExpr = prop.NavigationPrefix is null
                ? $"x => x.{prop.PropertyName}"
                : $"\"{prop.NavigationPrefix}_{prop.PropertyName}\"";

            var changedName = prop.NavigationPrefix is null
                ? $"nameof({entityName}.{prop.PropertyName})"
                : $"\"{prop.NavigationPrefix}.{prop.PropertyName}\"";

            if (prop.AlwaysAudit)
            {
                sb.AppendLine($"            changed.Add({changedName});");
                sb.AppendLine();
                continue;
            }

            if (prop.NavigationPrefix is not null)
            {
                sb.AppendLine($"            if (entry.Reference(x => x.{prop.NavigationPrefix}).TargetEntry?.Property(x => x.{prop.PropertyName}).IsModified == true)");
                sb.AppendLine($"                changed.Add({changedName});");
            }
            else
            {
                sb.AppendLine($"            if (entry.Property({propertyExpr}).IsModified)");
                sb.AppendLine($"                changed.Add({changedName});");
            }
        }
    }
}
