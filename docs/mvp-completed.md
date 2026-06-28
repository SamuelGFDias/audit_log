# MVP — AuditConfigurator\<TEntity\> ✅ Concluído

## O que foi construído

Biblioteca de auditoria com source generator Roslyn. Usuário declara quais propriedades auditar via `AuditConfigurator<TEntity>` e o generator produz o tipo `*AuditLog`, entity map, descriptor, registry e model builder extensions.

---

## DSL final

```csharp
[GenerateAuditLog]
public sealed partial class PacienteAuditConfigurator
    : AuditConfigurator<Paciente>
{
    public PacienteAuditConfigurator()
    {
        For(x => x.Id).Key();
        For(x => x.Nome).HasMaxLength(200).IsRequired();
        For(x => x.Cpf).Sensitive().HasMaxLength(11);
        For(x => x.CartaoSus).Sensitive();
        For(x => x.DataNascimento);
        For(x => x.DataAtualizacao).Ignore();
    }
}
```

## Artefatos gerados por entidade

| Entrada | Saída |
|---------|-------|
| `PacienteAuditConfigurator` | `PacienteAuditLog` (classe) |
| | `PacienteAuditLogEntityMap` (`IEntityTypeConfiguration`) |
| | `PacienteAuditLogDescriptor` (`IAuditorDescriptor`) |
| `ForEach(x => x.Medicamentos)` | `NotificacaoMedicamentoAuditLog` + EntityMap + Descriptor |
| — | `GeneratedAuditRegistryExtensions` |
| — | `GeneratedAuditModelBuilderExtensions` |

---

## Suporte do builder

- `For()` — propriedade auditada
- `.Key()` — vira `{EntityName}Id` (FK), não gera coluna extra
- `.Ignore()` — não entra no audit log
- `.Sensitive()` — valor mascarado como `"***"`
- `.HasMaxLength(n)` — configura coluna
- `.IsRequired()` — coluna NOT NULL
- `.WithColumnName("...")` — nome de coluna customizado
- `ForEach().ParentKey().Key().Configure()` — coleções aninhadas

---

## Estrutura do projeto

```
src/
├── AuditLog.Abstractions/           # Contratos runtime
│   ├── AuditConfigurator<TEntity>   # Classe base
│   ├── AuditPropertyBuilder         # For().Key().Sensitive()
│   ├── AuditCollectionBuilder       # ForEach().ParentKey()
│   ├── AuditOwnedBuilder            # ForOwned() (estrutura pronta)
│   ├── IAuditDescriptor             # Cria audit log
│   ├── AuditRegistry                # Registro de descritores
│   ├── AuditExecutionContext        # Contexto (usuário, correlation)
│   ├── EntityAuditHistory           # Retorno Entity + Logs
│   └── GenerateAuditLogAttribute    # Marcador
├── AuditLog.Generator/              # Source generator
│   ├── AuditLogGenerator.cs         # Entry point
│   ├── ConfiguratorDetector.cs      # Encontra [GenerateAuditLog]
│   ├── ExpressionParser.cs          # Parseia For/ForEach/ForOwned
│   ├── RootEntityGenerator.cs       # Gera *AuditLog, *EntityMap, *Descriptor
│   ├── CollectionEntityGenerator.cs # Gera coleções
│   ├── ExtensionGenerator.cs        # Registry + ModelBuilder
│   ├── Helpers.cs                   # Métodos compartilhados
│   └── Models.cs                    # PropertyConfig, CollectionConfig
└── AuditLog.EntityFrameworkCore/   # Integração EF Core
    ├── AuditSaveInterceptor          # Interceptor
    └── AuditLogServiceCollectionExtensions
```

---

## Testes

- **14** testes unitários (InMemory EF Core)
- **4** testes de integração (SQL Server real via TestContainers.MsSql)

### Cenários cobertos

- Insert → audit log com `"*"` em `CamposAlteradosJson`
- Update → `CamposAlteradosJson` lista campos modificados
- Delete → audit log gerado
- `.Sensitive()` → valor `"***"`
- `.Ignore()` → propriedade não entra
- `.Key()` → vira FK, sem duplicação
- `ForEach()` → tabela separada com ParentKey / ChildKey
- Registry registra todos os tipos
- ModelBuilder aplica todos os maps
- EntityAuditHistory tipado

---

## Decisões arquiteturais

### Sem DTO/Mapper

O configurador não mapeia para DTO intermediário. O source generator infere o audit log por convenção:

```
Paciente → PacienteAuditLog
Notificacao → NotificacaoAuditLog
NotificacaoMedicamento → NotificacaoMedicamentoAuditLog
```

### Tabela tipada, não genérica

Cada entidade tem sua própria tabela de audit log, com colunas reais. Não existe tabela `AuditLogs` com `EntityType`, `EntityId`, `Json` genérico. Isso permite queries tipadas, índices, migrations, sem `Deserialize<>`.

### Interceptor por reflection

O `AuditSaveInterceptor` usa `IAuditDescriptor<TEntity, TAuditLog>` via reflection para não exigir que o usuário registre cada descritor manualmente no interceptor. O registry centraliza o vínculo.
