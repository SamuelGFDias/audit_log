# AuditLog

[![Publish to NuGet](https://github.com/SamuelGFDias/audit_log/actions/workflows/publish.yml/badge.svg)](https://github.com/SamuelGFDias/audit_log/actions/workflows/publish.yml)

Biblioteca de auditoria automática para EF Core com Source Generators Roslyn.

## Pacotes

| Pacote | Descrição |
|--------|-----------|
| `AuditLog.Abstractions` | Contratos: `AuditConfigurator<T>`, `IAuditDescriptor`, builders |
| `AuditLog.EntityFrameworkCore` | Integração EF Core: `AuditSaveInterceptor`, extensions |
| `AuditLog.Generator` | Source generator Roslyn — gera `*AuditLog`, maps, descriptors |

## Uso

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
