# Bug — `CreateLog` gerado lança exceção pra entrada `Unchanged`-com-owned-alterado (regressão da 0.4.10)

## Contexto

A 0.4.10 corrigiu o bug descrito em
[interceptor-unchanged-owned-skip-fix.md](interceptor-unchanged-owned-skip-fix.md): `ProcessEntries`
agora **não pula mais** uma entrada raiz `Unchanged` quando ela tem uma referência owned alterada
(`HasChangedOwnedReference`). Isso resolveu o "log nem é criado" — mas expôs um segundo bug, numa
parte do código que o fix não tocou: o `CreateLog` **gerado** (`RootEntityGenerator.cs` e
`CollectionEntityGenerator.cs`) nunca esperava receber uma entrada em `EntityState.Unchanged`, então
lança exceção em vez de gerar o log.

## Bug identificado

| # | Gravidade | Onde | Problema |
|---|-----------|------|----------|
| 1 | **Crítico** (regressão) | `RootEntityGenerator.cs:137` e `CollectionEntityGenerator.cs:137` (código gerado, método `CreateLog`) | O `switch` que mapeia `entry.State` para `AuditOperation` só cobre `Added`/`Modified`/`Deleted`; qualquer outro estado cai no `_ => throw new InvalidOperationException($"Unexpected entity state: {entry.State}")`. Como `ProcessEntries` agora invoca `CreateLog` também para entradas `Unchanged` (com owned alterado), essa exceção passa a ser lançada **toda vez que só um owned type muda**, ao invés de gerar o log. |

## Reprodução

Confirmado empiricamente contra banco real (BIAE, `AuditLog` 0.4.10, interceptors reais), mesmo
cenário do doc anterior — raiz `Capacitacao` com owned type `Email? EmailResponsavelTecnico`,
alterando **só** o e-mail:

```csharp
capacitacao.AlterarDadosBasicos(..., emailResponsavelTecnico: new Email("novo@example.com"), ...);
context.ChangeTracker.DetectChanges();
// entry.State aqui: Unchanged (mesmo diagnóstico do doc anterior)
await context.SaveChangesAsync();
```

Stack trace observado:

```
Unhandled exception. System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.
 ---> System.InvalidOperationException: Unexpected entity state: Unchanged
   at Biae.Infra.Data.AuditConfigurators.CapacitacaoAuditLogDescriptor.CreateLog(EntityEntry`1 entry, AuditExecutionContext context) in .../CapacitacaoAuditLogDescriptor.g.cs:line 38
   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(...)
   ...
   at AuditLog.EntityFrameworkCore.AuditSaveInterceptor.ProcessEntries(DbContext context)
   at AuditLog.EntityFrameworkCore.AuditSaveInterceptor.SavingChangesAsync(...)
   at Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(...)
```

**Efeito prático:** antes da 0.4.10, essa edição simplesmente não gerava log (bug já documentado).
A partir da 0.4.10, a mesma edição agora **lança uma exceção não tratada e derruba o `SaveChanges`
inteiro** — regressão pior que o bug original, porque passa de "log ausente" pra "operação de
negócio falha".

BIAE contorna isso hoje porque já tinha um workaround local (em
`CapacitacaoService.EditarAsync`) que força `entry.Property(nameof(UpdatedAt)).IsModified = true`
quando detecta que a entrada ficaria `Unchanged` — isso promove `entry.State` pra `Modified` **antes**
do `SaveChanges`, então `ProcessEntries`/`CreateLog` nunca chegam a ver `Unchanged`. Mas esse
workaround é local a um único fluxo (`Capacitacao.EditarAsync`); qualquer outra entidade auditada no
BIAE (ou em qualquer consumidor do pacote) que tenha só um owned type alterado, sem esse workaround
manual, vai quebrar com essa exceção a partir da 0.4.10.

## Correção proposta

O jeito mais simples e sem efeito colateral: no `switch` gerado, tratar `EntityState.Unchanged` como
`Modified` — é semanticamente correto, porque `ProcessEntries` só chama `CreateLog` para uma entrada
`Unchanged` quando `HasChangedOwnedReference` já confirmou que algo mudou (então é sempre, de fato,
uma modificação do ponto de vista de auditoria).

### `src/AuditLog.Generator/RootEntityGenerator.cs` (linha 132-138) e `src/AuditLog.Generator/CollectionEntityGenerator.cs` (linha 132-138)

Trocar:

```csharp
var operacao = entry.State switch
{
    EntityState.Added => AuditLog.Abstractions.AuditOperation.Added,
    EntityState.Modified => AuditLog.Abstractions.AuditOperation.Modified,
    EntityState.Deleted => AuditLog.Abstractions.AuditOperation.Deleted,
    _ => throw new InvalidOperationException($"Unexpected entity state: {entry.State}")
};
```

por:

```csharp
var operacao = entry.State switch
{
    EntityState.Added => AuditLog.Abstractions.AuditOperation.Added,
    EntityState.Modified or EntityState.Unchanged => AuditLog.Abstractions.AuditOperation.Modified,
    EntityState.Deleted => AuditLog.Abstractions.AuditOperation.Deleted,
    _ => throw new InvalidOperationException($"Unexpected entity state: {entry.State}")
};
```

O restante do método (linha 140 em diante, `if (entry.State != EntityState.Added)`) já trata
qualquer estado diferente de `Added` da mesma forma (busca o log anterior pra `anteriorId`), então
`Unchanged` cai automaticamente no mesmo caminho de `Modified` sem precisar de mais mudanças ali.

**Alternativa descartada:** promover `entry.State = EntityState.Modified` dentro de
`AuditSaveInterceptor.ProcessEntries` antes de chamar `CreateLog`, em vez de mudar o gerador. Foi
descartada porque `entry.State = Modified` marca **todas** as propriedades escalares da entrada como
`IsModified` (comportamento do EF Core), o que gera um `UPDATE` desnecessário reescrevendo colunas
que não mudaram — e no caso do BIAE isso já causou um erro real de truncamento em
`CamposAlteradosJson` (coluna `nvarchar(255)`) quando testado como workaround alternativo. Mudar só
o `switch` do `CreateLog` gerado evita mexer em `entry.State`/no que é persistido — resolve só a
parte de auditoria, que é onde o bug está.

## Critério de aceite

- Teste cobrindo o cenário exato do bug anterior (raiz com owned type, mudando só o owned type):
  depois do fix de `interceptor-unchanged-owned-skip-fix.md`, `SaveChangesAsync` não deve lançar
  exceção, e deve gerar um log com `Operacao = Modified`.
- Nenhuma regressão nos testes existentes que cobrem `Added`/`Modified`/`Deleted` genuínos — esses
  devem continuar mapeando pra `Added`/`Modified`/`Deleted` normalmente.
- Vale revisar se `CollectionEntityGenerator.cs` (child collections, ex.: `CapacitacaoParticipante`)
  tem o mesmo cenário na prática (raiz filha só com owned type alterado) — o fix é o mesmo, só
  reforçando que os dois arquivos precisam da mesma mudança.

## Origem

Achado no BIAE (`new-biae/main`) ao validar a 0.4.10 — a mesma bateria de testes que confirmou o fix
de `interceptor-unchanged-owned-skip-fix.md` (log agora é criado quando outro campo muda junto)
também rodou o cenário isolado ("só o owned type muda, nada mais") pra fechar a validação, e foi aí
que a exceção apareceu. BIAE **mantém** o workaround local em `CapacitacaoService.EditarAsync`
(força `UpdatedAt.IsModified = true`) até esse fix ser publicado — sem ele, a 0.4.10 quebra o
`SaveChanges` nesse cenário ao invés de só pular o log.
