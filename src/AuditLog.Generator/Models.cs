#nullable enable
using System.Collections.Immutable;

namespace AuditLog.Generator;

internal sealed class PropertyConfig
{
    public string PropertyName { get; }
    public string? NavigationPrefix { get; }
    public string FullPropertyName => NavigationPrefix is null ? PropertyName : NavigationPrefix + PropertyName;
    public bool IsKey { get; }
    public bool IsIgnored { get; }
    public bool IsSensitive { get; }
    public bool AlwaysAudit { get; }
    public string? ColumnName { get; }
    public int MaxLength { get; }
    public bool IsRequired { get; }

    public PropertyConfig(
        string propertyName, string? navigationPrefix, bool isKey, bool isIgnored, bool isSensitive,
        bool alwaysAudit, string? columnName, int maxLength, bool isRequired)
    {
        PropertyName = propertyName;
        NavigationPrefix = navigationPrefix;
        IsKey = isKey;
        IsIgnored = isIgnored;
        IsSensitive = isSensitive;
        AlwaysAudit = alwaysAudit;
        ColumnName = columnName;
        MaxLength = maxLength;
        IsRequired = isRequired;
    }
}

internal sealed class CollectionConfig
{
    public string ElementName { get; }
    public string AuditLogName { get; }
    public string ParentKey { get; }
    public string ChildKey { get; }
    public ImmutableArray<PropertyConfig> ItemConfigs { get; }

    public CollectionConfig(
        string elementName, string auditLogName,
        string parentKey, string childKey,
        ImmutableArray<PropertyConfig> itemConfigs)
    {
        ElementName = elementName;
        AuditLogName = auditLogName;
        ParentKey = parentKey;
        ChildKey = childKey;
        ItemConfigs = itemConfigs;
    }
}

internal sealed class ConfiguratorInfo
{
    public string EntityNamespace { get; }
    public string ConfiguratorNamespace { get; }
    public string ConfiguratorName { get; }
    public string EntityName { get; }
    public string AuditLogName { get; }
    public ImmutableArray<PropertyConfig> Properties { get; }
    public ImmutableArray<CollectionConfig> Collections { get; }

    public ConfiguratorInfo(
        string entityNamespace, string configuratorNamespace,
        string configuratorName, string entityName,
        string auditLogName, ImmutableArray<PropertyConfig> properties,
        ImmutableArray<CollectionConfig> collections)
    {
        EntityNamespace = entityNamespace;
        ConfiguratorNamespace = configuratorNamespace;
        ConfiguratorName = configuratorName;
        EntityName = entityName;
        AuditLogName = auditLogName;
        Properties = properties;
        Collections = collections;
    }
}
