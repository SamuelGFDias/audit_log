# Plano: Refatoração dos Testes de SoftDelete

## Objetivo

Separar os testes de soft-delete em projetos específicos, removendo o `AuditLog.SoftDelete.IntegrationTests` genérico e organizando em:

1. **`AuditLog.SoftDelete.Reflection.Tests`** — apenas testes do caminho reflection (fallback)
2. **`AuditLog.SoftDelete.Generator.Tests`** — todos os testes de soft-delete (reflection + generator codegen), com o source generator registrado como analyzer

Os projetos `AuditLog.Tests` e `AuditLog.IntegrationTests` ficam exclusivos para o core da auditoria.

---

## Projetos

### 1. `AuditLog.SoftDelete.Reflection.Tests`

**Criado a partir** do `AuditLog.SoftDelete.IntegrationTests` existente, sem alterações nos testes.

```
tests/AuditLog.SoftDelete.Reflection.Tests/
├── AuditLog.SoftDelete.Reflection.Tests.csproj
├── DomainEntities.cs                     ← Paciente, Notificacao, etc (ISoftDeleteEntity)
└── SoftDeleteReflectionTests.cs          ← 8 testes existentes, sem [GenerateSoftDelete]
```

**csproj**: depende apenas do runtime `AuditLog.EntityFrameworkCore.SoftDelete`, sem referência ao generator.

### 2. `AuditLog.SoftDelete.Generator.Tests`

**Projeto novo** que referencia o generator como analyzer.

```
tests/AuditLog.SoftDelete.Generator.Tests/
├── AuditLog.SoftDelete.Generator.Tests.csproj
├── DomainEntities.cs                     ← mesmas entidades + [GenerateSoftDelete] no DbContext
├── SoftDeleteReflectionTests.cs          ← cópia dos 8 testes (fallback, sem registry)
└── SoftDeleteGeneratedTests.cs           ← 8 testes equivalentes usando generated handlers
```

**csproj**:
```xml
<ProjectReference Include="..\..\src\AuditLog.Generator.SoftDelete\AuditLog.Generator.SoftDelete.csproj"
                  ReferenceOutputAssembly="false" OutputItemType="Analyzer" />
```

O generator detecta `[GenerateSoftDelete]` no `TestDbContext` e gera `PacienteSoftDeleteHandler`, `NotificacaoSoftDeleteHandler`, etc. durante a compilação.

Os testes generated usam:
```csharp
var registry = new SoftDeleteHandlerRegistry();
registry.AddGeneratedSoftDeleteHandlers();
var interceptor = new SoftDeleteInterceptor(registry);
```

---

## Arquivos a criar/modificar/remover

| Ação | Caminho |
|------|---------|
| 🔴 Remover | `tests/AuditLog.SoftDelete.IntegrationTests/` (diretório inteiro) |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Reflection.Tests/AuditLog.SoftDelete.Reflection.Tests.csproj` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Reflection.Tests/DomainEntities.cs` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Reflection.Tests/SoftDeleteReflectionTests.cs` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Generator.Tests/AuditLog.SoftDelete.Generator.Tests.csproj` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Generator.Tests/DomainEntities.cs` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Generator.Tests/SoftDeleteReflectionTests.cs` |
| 🟢 Criar | `tests/AuditLog.SoftDelete.Generator.Tests/SoftDeleteGeneratedTests.cs` |
| 🔵 Modificar | `AuditLog.slnx` — remover `IntegrationTests`, adicionar `Reflection.Tests` e `Generator.Tests` |

---

## Testes incluídos

### Reflection tests (8 cenários — mesmo comportamento atual)

| # | Teste |
|---|-------|
| 1 | Should_mark_IsDeleted_and_DeletedAt_on_soft_delete |
| 2 | Should_cascade_soft_delete_to_children |
| 3 | Should_throw_RestrictDeleteViolationException_when_dependents_exist |
| 4 | Should_set_null_on_referencing_FK_when_SetNull |
| 5 | Should_filter_out_soft_deleted_entities_by_default |
| 6 | Should_return_all_entities_with_IgnoreQueryFilters |
| 7 | Should_not_throw_when_deleting_entity_without_dependents_Restrict |
| 8 | Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity |

### Generator tests (mesmos 8 cenários, via generated handlers)

Os mesmos 8 testes, mas com:
```csharp
var registry = new SoftDeleteHandlerRegistry();
registry.AddGeneratedSoftDeleteHandlers();
var interceptor = new SoftDeleteInterceptor(registry);
```

Validam que o código gerado pelo source compiler produz o mesmo comportamento que o reflection fallback.

---

## Dependências entre projetos

```
AuditLog.SoftDelete.Reflection.Tests
  └── AuditLog.EntityFrameworkCore.SoftDelete  (runtime)

AuditLog.SoftDelete.Generator.Tests
  ├── AuditLog.EntityFrameworkCore.SoftDelete  (runtime)
  └── AuditLog.Generator.SoftDelete            (analyzer, ReferenceOutputAssembly=false)
       └── AuditLog.Generator.Shared           (transitivo pelo analyzer)
```

---

## Total de testes esperado

| Projeto | Testes |
|---------|--------|
| `AuditLog.Tests` | 17 |
| `AuditLog.IntegrationTests` | 4 |
| `AuditLog.SoftDelete.Reflection.Tests` | 8 |
| `AuditLog.SoftDelete.Generator.Tests` | 16 (8 reflection + 8 generated) |
| **Total** | **45** |
