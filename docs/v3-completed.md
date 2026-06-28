# V3 — AuditOperation enum ✅ Concluído

## O que mudou

`Operacao` deixou de ser `string` e passou a ser um enum `AuditOperation`, definido em `AuditLog.Abstractions`.

---

## Enum

```csharp
namespace AuditLog.Abstractions;

public enum AuditOperation
{
    Added = 0,
    Modified = 1,
    Deleted = 2
}
```

## Geração

### AuditLog class

```csharp
// Antes
public string Operacao { get; set; } = null!;

// Depois
public AuditLog.Abstractions.AuditOperation Operacao { get; set; }
```

### EntityMap

```csharp
builder.Property(x => x.Operacao)
    .HasMaxLength(30)
    .IsRequired()
    .HasConversion<string>();  // persiste como string no banco
```

### Descriptor (CreateLog)

```csharp
var operacao = entry.State switch
{
    EntityState.Added => AuditOperation.Added,
    EntityState.Modified => AuditOperation.Modified,
    EntityState.Deleted => AuditOperation.Deleted,
    _ => throw new InvalidOperationException(...)
};
```

## Testes

- **17** testes unitários (InMemory)
- **4** testes de integração (SQL Server via TestContainers)
- Todos os testes que comparavam `"Added"` / `"Modified"` / `"Deleted"` com string foram atualizados para `AuditOperation.Added` / `AuditOperation.Modified` / `AuditOperation.Deleted`
