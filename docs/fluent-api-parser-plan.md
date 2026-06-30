# Plano: Substituir detecção por convenção/atributo por Fluent API parsing

## Problema

O SoftDelete Generator atual exige **atributos** no domínio (`[DeleteBehavior(Cascade)]` em navigation properties), o que polui as entidades com dependência da biblioteca de auditoria.

## Objetivo

Eliminar `[DeleteBehavior]` attribute e toda detecção por convenção. O generator passa a inspecionar a **Fluent API do EF Core** dentro do `OnModelCreating` para extrair relacionamentos e `OnDelete()`.

---

## O que muda

### Antes (atual)

```csharp
// Domínio poluído
public class Paciente : ISoftDeleteEntity
{
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public List<PacienteDocumento> Documentos { get; set; }
}
```

O `EntityDetector` escaneia propriedades da entidade em busca de:
- Propriedades terminadas em `Id` → infere FK por convenção
- Atributos `[DeleteBehavior]` → extrai behavior
- Coleções → detecta relacionamento

### Depois

```csharp
// Domínio puro — sem atributos
public class Paciente : ISoftDeleteEntity
{
    public List<PacienteDocumento> Documentos { get; set; }
}
```

```csharp
// Fluent API no DbContext — única fonte de verdade
protected override void OnModelCreating(ModelBuilder mb)
{
    mb.Entity<Paciente>(e =>
    {
        e.HasMany(x => x.Documentos)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);
    });
}
```

O `EntityDetector` é substituído por um **FluentApiParser** que percorre a `SyntaxTree` do `OnModelCreating`.

---

## Arquivos a modificar/criar

| # | Arquivo | Ação |
|---|---------|------|
| 1 | `src/AuditLog.Generator.SoftDelete/FluentApiParser.cs` | **Criar** — parseia chamadas Entity/ HasOne/ HasMany/ OnDelete |
| 2 | `src/AuditLog.Generator.SoftDelete/EntityDetector.cs` | **Modificar** — remover lógica de atributo + convenção, usar FluentApiParser |
| 3 | `src/AuditLog.Generator.SoftDelete/SoftDeleteGenerator.cs` | **Modificar** — pipeline: primeiro detectar DbContext, depois parsear OnModelCreating |
| 4 | `tests/AuditLog.SoftDelete.Generator.Tests/DomainEntities.cs` | **Modificar** — remover `[DeleteBehavior]` attributes |
| 5 | `src/AuditLog.EntityFrameworkCore.SoftDelete/DeleteBehaviorAttribute.cs` | **Remover** (opcional, não usado mais) |

---

## 1. FluentApiParser

Classe que analisa a `SyntaxTree` do método `OnModelCreating` e extrai:

- Entidade principal (`Entity<T>`)
- Propriedade de navegação (`HasMany(x => x.Documentos)`)
- Tipo de relacionamento (`HasOne` / `HasMany`)
- FK (`HasForeignKey(x => x.PacienteId)`)
- Delete behavior (`OnDelete(DeleteBehavior.Restrict)`)

```csharp
internal sealed class RelationshipConfig
{
    public string PrincipalEntity { get; init; }     // "Paciente"
    public string NavigationProperty { get; init; }  // "Documentos"
    public string DependentEntity { get; init; }     // "PacienteDocumento"
    public string FkProperty { get; init; }           // "PacienteId"
    public bool FkIsNullable { get; init; }
    public string DeleteBehavior { get; init; }       // "Cascade" | "Restrict" | "SetNull"
    public bool IsOwnership { get; init; }
}
```

### Pipeline de parsing

```
OnModelCreating method body
  └─ ExpressionStatement → InvocationExpression
       └─ Cadeia de .Entity<T>().HasMany().WithOne().HasForeignKey().OnDelete()
            ├─ GenericNameSyntax("Entity") → Extrai T → PrincipalEntity
            ├─ "HasMany" | "HasOne" → Extrai lambda → NavigationProperty
            ├─ "WithOne" | "WithMany" → determina direção
            ├─ "HasForeignKey" → Extrai lambda → FkProperty
            ├─ "OnDelete" → Extrai MemberAccess → DeleteBehavior
            └─ "OwnsOne" | "OwnsMany" → IsOwnership = true
```

### Extração de DeleteBehavior

```csharp
// .OnDelete(DeleteBehavior.Restrict)
// Parseia o argumento: MemberAccessExpressionSyntax → "Restrict"
private static string? ExtractDeleteBehavior(InvocationExpressionSyntax invocation)
{
    var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
    if (arg is MemberAccessExpressionSyntax ma)
        return ma.Name.Identifier.Text; // "Cascade", "Restrict", "SetNull"
    if (arg is IdentifierNameSyntax ins)
    {
        return ins.Identifier.Text switch
        {
            "Cascade" or "Restrict" or "SetNull" => ins.Identifier.Text,
            _ => null
        };
    }
    return null;
}
```

### Extração de FK

```csharp
// .HasForeignKey(x => x.PacienteId)
// Extrai o nome da propriedade da lambda
private static string? ExtractForeignKey(InvocationExpressionSyntax invocation)
{
    var arg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
    if (arg is SimpleLambdaExpressionSyntax { Body: MemberAccessExpressionSyntax ma })
        return ma.Name.Identifier.Text;
    return null;
}
```

### Tratamento de `OwnsOne` / `OwnsMany`

```csharp
if (methodName is "OwnsOne" or "OwnsMany")
{
    // Owned types são ignorados — não participam de soft delete
    // Mas precisamos detectá-los para não tentar criar handlers
    continue;
}
```

---

## 2. EntityDetector — modificações

O `EntityDetector` atual faz 3 coisas:
1. Detecta se uma classe é candidata (`ISoftDeleteEntity`, `[GenerateSoftDelete]`)
2. Detecta PK (`Id`, `{Name}Id`)
3. Detecta relacionamentos (convenção + atributos)

Com o FluentApiParser, as responsabilidades mudam:

```diff
- EntityDetector.AnalyzeEntity() → escaneia propriedades da entidade
+ EntityDetector.AnalyzeEntity() → detecta PK e ISoftDeleteEntity, relacionamentos vêm do FluentApiParser
```

O detector de PK continua em `AnalyzeEntity` (escaneia propriedades, acha `Id` / `{Name}Id`).

O detector de relacionamentos é removido de `AnalyzeEntity` e movido para `FluentApiParser.ProcessDbContext()`.

---

## 3. SoftDeleteGenerator — pipeline modificado

```csharp
public void Initialize(IncrementalGeneratorInitializationContext context)
{
    // Pipeline 1: Detecta DbContexts com [GenerateSoftDelete]
    var dbContexts = context.SyntaxProvider
        .CreateSyntaxProvider(
            predicate: EntityDetector.IsDbContextCandidate,
            transform: (ctx, _) =>
            {
                // 1a. Detecta entidades via DbSet<>
                var entities = EntityDetector.GetDbSetEntities(ctx);
                // 1b. Parseia OnModelCreating para extrair relacionamentos
                var relationships = FluentApiParser.ParseOnModelCreating(ctx);
                // 1c. Combina: associa relacionamentos às entidades
                return (entities, relationships);
            })
        .Where(m => m.entities.Length > 0)
        .Collect();

    context.RegisterSourceOutput(dbContexts, GenerateAll);
}
```

---

## 4. Testes — remoção de atributos

```diff
- [AuditLog.EntityFrameworkCore.SoftDelete.DeleteBehavior(CascadeDeleteBehavior.Cascade)]
  public List<Notificacao> Notificacoes { get; set; } = [];
- [AuditLog.EntityFrameworkCore.SoftDelete.DeleteBehavior(CascadeDeleteBehavior.Restrict)]
  public List<PacienteDocumento> Documentos { get; set; } = [];
```

Os behaviors agora vêm exclusivamente do `OnDelete()` no `OnModelCreating`.

---

## 5. Relatórios de erros

Se o FluentApiParser não encontrar configuração explícita via Fluent API, deve emitir **diagnóstico**:

```csharp
context.ReportDiagnostic(Diagnostic.Create(
    new DiagnosticDescriptor(
        "SG001", "Missing relationship configuration",
        "Entity '{0}' has navigation '{1}' to '{2}' but no Fluent API configuration was found",
        "SoftDelete", DiagnosticSeverity.Warning, true),
    location));
```

Isso orienta o desenvolvedor a adicionar a configuração Fluent API.

---

## Resumo

| Aspecto | Antes | Depois |
|---------|-------|--------|
| Como detecta FK | Propriedade `*Id` | Fluent API `HasForeignKey()` |
| Como detecta behavior | Atributo `[DeleteBehavior]` | Fluent API `OnDelete()` |
| Como detecta relacionamento | Atributo + convenção de navegação | Fluent API `HasOne/HasMany` |
| Domínio poluído? | Sim — atributos na entidade | Não — POCO puro |
| Testes Restrict falhando? | Sim | Provavelmente corrigido |

---

## Ordem de implementação

| # | Passo | Arquivos |
|---|-------|----------|
| 1 | Criar `FluentApiParser.cs` com parsing de Entity/ HasOne/ HasMany/ HasForeignKey/ OnDelete/ OwnsOne | `FluentApiParser.cs` |
| 2 | Modificar `EntityDetector.cs`: remover `InferDeleteBehavior`, `HasCollectionType`, `GetCollectionElementType`, `FindFkOnTarget`; simplificar `AnalyzeEntity` para só detectar PK + ISoftDeleteEntity | `EntityDetector.cs` |
| 3 | Modificar `SoftDeleteGenerator.cs`: pipeline usa FluentApiParser para extrair relaciones e mescla com EntityDetector | `SoftDeleteGenerator.cs` |
| 4 | Modificar testes: remover `[DeleteBehavior]` attributes | `DomainEntities.cs` |
| 5 | Build + testes de integração | — |
