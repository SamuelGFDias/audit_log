# SoftDelete — Biblioteca opcional e independente

## Objetivo

Criar uma biblioteca separada (`AuditLog.EntityFrameworkCore.SoftDelete`) que forneça soft-delete automático para entidades EF Core, com suporte a cascade, restrict e set-null. Deve ser **opcional** — o usuário escolhe usar ou não — e **componível** com o `AuditSaveInterceptor` existente.

---

## Referência

A implementação de referência está em `/home/samueldias/dev/back/sindras/main`:

- `src/Sindras.Domain.Shared/Interfaces/IAuditEntity.cs` — interface com `CreatedAt`, `UpdatedAt`, `DeletedAt`, `IsDeleted`
- `src/Sindras.Domain.Shared/Models/Entity.cs` — classe base `AuditEntity`
- `src/Sindras.Infra.Data/Extensions/SoftDeleteByBehaviorHandler.cs` — handler de cascade/restrict/setnull
- `src/Sindras.Infra.Data/Contexts/AppDbContext.cs` — aplica global query filter `!IsDeleted` + unique index filter

---

## Design da nova biblioteca

### 1. Interface `ISoftDeleteEntity`

Apenas o essencial para soft-delete, sem timestamps de criação/atualização:

```csharp
public interface ISoftDeleteEntity
{
    DateTime? DeletedAt { get; set; }
    bool IsDeleted { get; set; }
}
```

### 2. `SoftDeleteInterceptor` — SaveChangesInterceptor

Funciona como o `AuditSaveInterceptor`: registra-se no `DbContextOptionsBuilder.AddInterceptors()`.

**Comportamento:**
- Escaneia `ChangeTracker.Entries<ISoftDeleteEntity>()` em busca de entradas com `State == EntityState.Deleted`
- Altera para `EntityState.Modified` e seta `IsDeleted = true`, `DeletedAt = now`
- Delega para `SoftDeleteCascadeHandler` o tratamento de FKs referenciando a entidade deletada

### 3. `SoftDeleteCascadeHandler` — Cascade/Restrict/SetNull

Adaptado do `SoftDeleteByBehaviorHandler` do sindras:

- `HandleDeleteAsync(DbContext, EntityEntry, DateTime)` — entry point
- `MarkSoftDeleted(EntityEntry, DateTime)` — marca a entidade + owned types
- `ValidateRestrictAsync(DbContext, EntityEntry, IForeignKey)` — verifica se há dependentes, lança `RestrictDeleteViolationException`
- `ProcessCascadeAsync(...)` — soft-deleta dependentes recursivamente
- `ProcessSetNullAsync(...)` — seta FK como null nos dependentes

### 4. `SoftDeleteQueryFilterExtensions` — Global query filter

```csharp
modelBuilder.ApplySoftDeleteQueryFilter();      // global !IsDeleted
modelBuilder.ApplySoftDeleteUniqueIndexFilter(); // [IsDeleted] = 0 em índices únicos
```

### 5. DI registration

```csharp
services.AddSoftDelete(); // registra SoftDeleteInterceptor como singleton
```

---

## Arquivos a criar

```
src/AuditLog.EntityFrameworkCore.SoftDelete/
├── AuditLog.EntityFrameworkCore.SoftDelete.csproj
├── ISoftDeleteEntity.cs
├── SoftDeleteInterceptor.cs
├── SoftDeleteCascadeHandler.cs
├── SoftDeleteQueryFilterExtensions.cs
├── SoftDeleteServiceCollectionExtensions.cs
├── RestrictDeleteViolationException.cs

tests/AuditLog.SoftDelete.IntegrationTests/
├── AuditLog.SoftDelete.IntegrationTests.csproj
├── DomainEntities.cs
├── SoftDeleteIntegrationTests.cs

docs/soft-delete-plan.md          ← este arquivo
```

---

## Comportamento esperado

### 📌 Soft-delete simples
```csharp
db.Pacientes.Remove(paciente);
await db.SaveChangesAsync();
// → paciente.IsDeleted == true
// → paciente.DeletedAt != null
// → registro deletado fisicamente NÃO existe no banco (foi convertido para Modified)
```

### 📌 Cascade
```csharp
db.Pacientes.Remove(paciente);
await db.SaveChangesAsync();
// → paciente.IsDeleted = true
// → notificacoes vinculadas (FK → Paciente com Cascade) também IsDeleted = true
// → owned types do paciente marcados como Unchanged (blindados)
```

### 📌 Restrict
```csharp
db.Pacientes.Remove(paciente);
await db.SaveChangesAsync();
// → se houver dependentes, lança RestrictDeleteViolationException
```

### 📌 SetNull
```csharp
db.Pacientes.Remove(paciente);
await db.SaveChangesAsync();
// → FK nos dependentes setada como null
```

### 📌 Query filter
```csharp
db.Pacientes.ToList(); // só retorna IsDeleted == false
db.Pacientes.IgnoreQueryFilters().ToList(); // retorna todos
```

### 📌 Composição com AuditSaveInterceptor
```csharp
optionsBuilder.AddInterceptors(auditSaveInterceptor);
optionsBuilder.AddInterceptors(softDeleteInterceptor);
```
Ambos podem coexistir. O soft-delete converte `Deleted → Modified` antes do audit interceptor (depende da ordem de registro).

---

## Testes de integração

Usar TestContainers.MsSql (mesmo padrão dos testes existentes).

### Cenários:

| # | Teste | Validação |
|---|-------|-----------|
| 1 | Soft-delete marca IsDeleted e DeletedAt | `IsDeleted == true`, `DeletedAt != null` |
| 2 | Cascade soft-delete | Dependente também `IsDeleted == true` |
| 3 | Restrict lança exceção | `RestrictDeleteViolationException` |
| 4 | SetNull na FK | FK do dependente fica `null` |
| 5 | Query filter filtra deletados | `Count()` retorna só não-deletados |
| 6 | IgnoreQueryFilters retorna todos | `IgnoreQueryFilters().Count()` inclui deletados |
| 7 | Owned types não são afetados | Owned type permanece Unchanged |
| 8 | Composição com audit interceptor | Ambos executam sem conflito |

---

## Estrutura do banco nos testes

```sql
CREATE TABLE Pacientes (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Nome NVARCHAR(200) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL
);

CREATE TABLE Notificacoes (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    PacienteId UNIQUEIDENTIFIER NOT NULL,
    Diagnostico NVARCHAR(MAX) NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_Notificacoes_Pacientes
        FOREIGN KEY (PacienteId) REFERENCES Pacientes(Id)
        ON DELETE CASCADE  -- ou Restrict, ou SetNull
);

CREATE TABLE PacienteDocumentos (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    PacienteId UNIQUEIDENTIFIER NOT NULL,
    NomeDocumento NVARCHAR(200),
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT FK_PacienteDocumentos_Pacientes
        FOREIGN KEY (PacienteId) REFERENCES Pacientes(Id)
        ON DELETE CASCADE
);
```
