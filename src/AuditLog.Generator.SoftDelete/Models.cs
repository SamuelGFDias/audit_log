#nullable enable
using System.Collections.Immutable;

namespace AuditLog.Generator.SoftDelete;

internal sealed class FkProperty
{
    public string PropertyName { get; }
    public string PropertyType { get; }
    public bool IsNullable { get; }
    public string TargetEntityName { get; }
    public string DeleteBehavior { get; }

    public FkProperty(
        string propertyName, string propertyType, bool isNullable,
        string targetEntityName, string deleteBehavior)
    {
        PropertyName = propertyName;
        PropertyType = propertyType;
        IsNullable = isNullable;
        TargetEntityName = targetEntityName;
        DeleteBehavior = deleteBehavior;
    }
}

internal sealed class RelationshipInfo
{
    public string DependentEntityFullName { get; }
    public string DependentEntityName { get; }
    public string FkPropertyName { get; }
    public string FkPropertyType { get; }
    public string PrincipalKeyName { get; }
    public bool FkIsNullable { get; }
    public string DeleteBehavior { get; }
    public bool IsOwnership { get; }
    public bool DependentIsSoftDelete { get; }

    public RelationshipInfo(
        string dependentEntityFullName, string dependentEntityName,
        string fkPropertyName, string fkPropertyType,
        string principalKeyName, bool fkIsNullable,
        string deleteBehavior, bool isOwnership, bool dependentIsSoftDelete)
    {
        DependentEntityFullName = dependentEntityFullName;
        DependentEntityName = dependentEntityName;
        FkPropertyName = fkPropertyName;
        FkPropertyType = fkPropertyType;
        PrincipalKeyName = principalKeyName;
        FkIsNullable = fkIsNullable;
        DeleteBehavior = deleteBehavior;
        IsOwnership = isOwnership;
        DependentIsSoftDelete = dependentIsSoftDelete;
    }
}

internal sealed class EntityInfo
{
    public string Namespace { get; }
    public string Name { get; }
    public string FullName { get; }
    public string PrimaryKeyType { get; }
    public string PrimaryKeyName { get; }
    public bool IsSoftDelete { get; }
    public ImmutableArray<FkProperty> OwnFkProperties { get; }
    public ImmutableArray<RelationshipInfo> ReferencingFks { get; internal set; }

    public EntityInfo(
        string ns, string name, string fullName,
        string primaryKeyType, string primaryKeyName, bool isSoftDelete,
        ImmutableArray<FkProperty> ownFkProperties,
        ImmutableArray<RelationshipInfo> referencingFks = default)
    {
        Namespace = ns;
        Name = name;
        FullName = fullName;
        PrimaryKeyType = primaryKeyType;
        PrimaryKeyName = primaryKeyName;
        IsSoftDelete = isSoftDelete;
        OwnFkProperties = ownFkProperties;
        ReferencingFks = referencingFks.IsDefaultOrEmpty
            ? ImmutableArray<RelationshipInfo>.Empty
            : referencingFks;
    }
}

internal sealed class DbContextInfo
{
    public string Namespace { get; }
    public string Name { get; }
    public string FullName { get; }
    public ImmutableArray<EntityInfo> Entities { get; }

    public DbContextInfo(
        string ns, string name, string fullName,
        ImmutableArray<EntityInfo> entities)
    {
        Namespace = ns;
        Name = name;
        FullName = fullName;
        Entities = entities;
    }
}
