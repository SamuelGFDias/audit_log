# V2 — ForOwned / AlwaysAudit / AuditLogAnteriorId ✅ Concluído

## Escopo

Três features que formam a segunda iteração da biblioteca de auditoria.

---

## 1. `ForOwned<TOwned>()` — owned types achatados

Permite que propriedades de um owned type sejam achatadas na tabela de audit log da entidade raiz, em vez de gerar uma tabela separada.

### DSL

```csharp
ForOwned(x => x.Endereco, o =>
{
    o.For(x => x.Logradouro).HasMaxLength(200);
    o.For(x => x.Cidade);
    o.For(x => x.Cep).Sensitive();
});
```

### Comportamento

- As propriedades do owned type viram colunas na tabela `*AuditLog` da entidade raiz
- Prefixo de navegação nas colunas (ex: `EnderecoLogradouro`, `EnderecoCidade`, `EnderecoCep`)
- `o.For()` retorna `AuditPropertyBuilder<TEntity, TProperty>` (mesmo builder já existente)
- Suporta todos os modificadores: `.Sensitive()`, `.HasMaxLength()`, `.IsRequired()`, `.WithColumnName()`

### Geração

- `PacienteAuditLog` ganha colunas: `EnderecoLogradouro`, `EnderecoCidade`, `EnderecoCep`
- `PacienteAuditLogDescriptor` mapeia `entity.Endereco.Logradouro` para `EnderecoLogradouro`
- `PacienteAuditLogEntityMap` configura as colunas com os modificadores

### Testes

- 16 testes unitários (InMemory EF Core)
- 4 testes de integração (SQL Server real via TestContainers)
- Cenários: ForOwned com owned types + sensitive + required, AlwaysAudit com e sem modificação, AuditLogAnteriorId em cadeia Added → Modified → Modified

---

## 2. `AlwaysAudit()` — auditar mesmo sem alteração

Propriedades marcadas com `.AlwaysAudit()` devem SEMPRE aparecer em `CamposAlteradosJson`, independente de terem sido modificadas ou não.

### DSL

```csharp
For(x => x.Situacao)
    .AlwaysAudit();
```

### Comportamento

- No `BuildChangedPropertiesJson()` do descriptor gerado, propriedades `AlwaysAudit` são incluídas incondicionalmente na lista
- Mesmo que `entry.Property(x => x.Situacao).IsModified` seja `false`, `"Situacao"` aparece no JSON
- Útil para campos como `Situacao` que devem constar no histórico mesmo quando o trigger é outra alteração

### Geração

Antes:
```csharp
if (entry.Property(x => x.Situacao).IsModified)
    changed.Add(nameof(Paciente.Situacao));
```

Depois:
```csharp
// AlwaysAudit — sempre incluída
changed.Add(nameof(Paciente.Situacao));
```

### Testes

- 16 testes unitários (InMemory EF Core)
- 4 testes de integração (SQL Server real via TestContainers)
- Cenários: AlwaysAudit com e sem modificação em `CamposAlteradosJson`

---

## 3. `AuditLogAnteriorId` — diff via auto-relacionamento

Cada `*AuditLog` ganha uma FK opcional para a versão anterior da mesma entidade. A própria linha já é o snapshot completo — o "antes" é a linha anterior, acessível via `.Include()`.

### Schema na tabela gerada

```
*AuditLog
├── Id                  long            PK
├── {Entity}Id          Guid            FK da entidade auditada
├── Operacao            string          "Added"|"Modified"|"Deleted"
├── OcorridoEm          DateTimeOffset
├── AuditLogAnteriorId  long?           nullable, FK → *AuditLog.Id
├── AuditLogAnterior    *AuditLog?      navigation property (optional)
├── Nome                string?         snapshot atual
├── Cpf                 string?         "***" se sensitive
└── ...
```

### Comportamento por operação

| Operação | AuditLogAnteriorId |
|----------|--------------------|
| `Added`  | `null` (primeira versão) |
| `Modified` | Id da linha anterior (Added ou Modified) |
| `Deleted` | Id da linha anterior |

### Uso prático

```csharp
var atual = await db.Set<PacienteAuditLog>()
    .Include(x => x.AuditLogAnterior)
    .FirstAsync(x => x.PacienteId == id && x.Operacao == "Modified");

// atual.Nome = "João Silva"     (valor NOVO)
// atual.AuditLogAnterior!.Nome  = "João"  (valor ANTES)
```

### Vantagens sobre JSON

- Zero duplicação (cada valor está em uma coluna real)
- Consulta via `Include()` sem parsing de JSON
- Schema fortemente tipado, sem `Deserialize<>`
- Funciona com migration e migrations history
- A primeira versão (Added) pode armazenar valores reais se necessário

### Desvantagem

Sensitive fields ficam mascarados em todas as versões (incluindo a anterior via Include). Para obter o valor original do Cpf, seria necessário uma coluna extra não-mascarada ou manter a primeira versão sem máscara.

### Testes

- 16 testes unitários (InMemory EF Core)
- 4 testes de integração (SQL Server real via TestContainers)
- Cenários: cadeia Added → Modified → Modified com `.Include(x => x.AuditLogAnterior)`
