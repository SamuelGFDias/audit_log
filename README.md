# AuditLog

[![Publish to NuGet](https://github.com/SamuelGFDias/audit_log/actions/workflows/publish.yml/badge.svg)](https://github.com/SamuelGFDias/audit_log/actions/workflows/publish.yml)

Biblioteca de auditoria automática para EF Core com Source Generators Roslyn.

## Pacotes

| Pacote | Descrição |
|--------|-----------|
| `AuditLog.Abstractions` | Contratos: `AuditConfigurator<T>`, `IAuditDescriptor`, builders |
| `AuditLog.EntityFrameworkCore` | Integração EF Core: `AuditSaveInterceptor`, extensions |
| `AuditLog.Generator` | Source generator — gera `*AuditLog`, maps, descriptors |
| `AuditLog.EntityFrameworkCore.SoftDelete` | Runtime: interfaces, interceptor, query filters para soft delete |
| `AuditLog.Generator.SoftDelete` | Source generator — gera handlers tipados de cascade/restrict/set-null |

---

## AuditLog — Auditoria de Entidades

### 1. Defina um configurador

```csharp
[GenerateAuditLog]
public sealed class PacienteAuditConfigurator : AuditConfigurator<Paciente>
{
    public PacienteAuditConfigurator()
    {
        For(x => x.Id).Key();
        For(x => x.Nome).HasMaxLength(200).IsRequired();
        For(x => x.Cpf).Sensitive().HasMaxLength(11);
        For(x => x.DataAtualizacao).Ignore();
    }
}
```

### 2. Adicione o interceptor no DbContext

```csharp
public class AppDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new AuditSaveInterceptor());
    }
}
```

### 3. O generator produz automaticamente

- `PacienteAuditLog` — tabela de auditoria com snapshot dos dados
- `PacienteAuditLogDescriptor` — mapeia `Paciente → PacienteAuditLog`
- `PacienteAuditLogEntityMap` — EF Core configuration (column types, max length)
- `ServiceCollectionExtensions.AddGeneratedAuditLogs()` — DI registration

---

## SoftDelete — Exclusão Lógica

Dois pacotes complementares:

| Package | Função |
|---------|--------|
| `AuditLog.EntityFrameworkCore.SoftDelete` | Runtime: interceptor, interfaces, query filters |
| `AuditLog.Generator.SoftDelete` | Source generator (opcional): gera handlers com tipagem forte |

### Instalação

```xml
<ItemGroup>
  <PackageReference Include="AuditLog.EntityFrameworkCore.SoftDelete" Version="1.0.0" />
  <PackageReference Include="AuditLog.Generator.SoftDelete" Version="1.0.0" />
</ItemGroup>
```

### 1. Implemente `ISoftDeleteEntity` nas entidades

```csharp
public class Paciente : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; }

    // Obrigatório para soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Relacionamentos (Fluent API configurada no DbContext)
    public List<Notificacao> Notificacoes { get; set; } = [];
}
```

### 2. Marque o DbContext com `[GenerateSoftDelete]`

```csharp
using AuditLog.EntityFrameworkCore.SoftDelete;

[GenerateSoftDelete]
public class AppDbContext : DbContext
{
    public DbSet<Paciente> Pacientes { get; set; }
    public DbSet<Notificacao> Notificacoes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Paciente>(e =>
        {
            e.HasMany(x => x.Notificacoes)
                .WithOne(x => x.Paciente)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
```

### 3. Registre na DI

```csharp
// Com o source generator (handlers tipados)
var registry = new SoftDeleteHandlerRegistry();
registry.AddGeneratedSoftDeleteHandlers();
services.AddSoftDelete(registry);

// Ou sem o generator (fallback via reflection)
services.AddSoftDelete();
```

### 4. Use normalmente

```csharp
db.Pacientes.Remove(paciente);
await db.SaveChangesAsync();
// → IsDeleted = true, DeletedAt = now
// → Cascade: Notificacoes também são marcadas como deletadas
// → Query filter global: db.Pacientes retorna apenas não-deletados
```

### Comportamentos por FK

| `OnDelete()` | Efeito |
|---|---|
| `Cascade` | Dependentes são soft-deletados recursivamente |
| `Restrict` | Lança `RestrictDeleteViolationException` se houver dependentes |
| `SetNull` | FK dos dependentes é setada como `null` |

### Convenções (quando `OnDelete` não é especificado)

| Navigation | Comportamento |
|---|---|
| Collection (`List<T>`) | `Cascade` |
| Reference (`T`) | `Restrict` |
| FK nullable (`Guid?`) | `SetNull` |

### Consultas

```csharp
// Query filter automático — só não-deletados
db.Pacientes.ToList();

// Incluir deletados
db.Pacientes.IgnoreQueryFilters().ToList();
```

### Suporte a herança indireta de `IEntityTypeConfiguration<T>`

O gerador detecta entity maps que implementam `IEntityTypeConfiguration<T>` através de toda a cadeia de herança, incluindo casos como `AuditEntityMap<T>` → `IContextEntityMap<T>` → `IEntityTypeConfiguration<T>`. Isso funciona tanto com `ApplyConfiguration(new ConcreteEntityMap())` quanto com `ApplyConfigurationsFromAssembly()`.
