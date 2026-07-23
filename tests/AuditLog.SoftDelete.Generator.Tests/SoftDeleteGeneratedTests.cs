using AuditLog.EntityFrameworkCore.SoftDelete;
using AuditLog.TestContainers.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLog.SoftDelete.Generator.Tests;

public sealed class SoftDeleteGeneratedTests
{
    private static string ConnectionStringFor(string dbName)
        => MsSqlContainerFixture.GetConnectionString(dbName);

    private SoftDeleteInterceptor InterceptorWithGeneratedHandlers()
    {
        var registry = new SoftDeleteHandlerRegistry();
        registry.AddGeneratedSoftDeleteHandlers();
        return new SoftDeleteInterceptor(registry);
    }

    // ── DbContext inline ──────────────────────────────────────

    [Fact]
    public async Task Inline_Should_set_null_on_SetNull_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Inline_Should_set_null_on_SetNull_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "SN", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.HistoricoEnderecos.Add(new EnderecoHistorico { Id = Guid.NewGuid(), Logradouro = "Rua X", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.Null((await db.Set<EnderecoHistorico>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.HistoricoEnderecos[0].Id)).PacienteId);
    }

    [Fact]
    public async Task Inline_Should_throw_Restrict_when_dependents_exist()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Inline_Should_throw_Restrict_when_dependents_exist)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "R", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Documentos.Add(new PacienteDocumento { Id = Guid.NewGuid(), NomeDocumento = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p);
        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(() => db.SaveChangesAsync());
        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task Inline_Should_cascade_to_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Inline_Should_cascade_to_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "M", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Notificacoes.Add(new Notificacao { Id = Guid.NewGuid(), Diagnostico = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.True((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Notificacoes[0].Id)).IsDeleted);
    }

    // ── DbContext com entity maps ─────────────────────────────

    [Fact]
    public async Task EntityMap_Should_set_null_on_SetNull_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithMaps>()
            .UseSqlServer(ConnectionStringFor(nameof(EntityMap_Should_set_null_on_SetNull_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithMaps(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "Maps", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.HistoricoEnderecos.Add(new EnderecoHistorico { Id = Guid.NewGuid(), Logradouro = "Rua X", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.Null((await db.Set<EnderecoHistorico>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.HistoricoEnderecos[0].Id)).PacienteId);
    }

    [Fact]
    public async Task EntityMap_Should_throw_Restrict_when_dependents_exist()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithMaps>()
            .UseSqlServer(ConnectionStringFor(nameof(EntityMap_Should_throw_Restrict_when_dependents_exist)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithMaps(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "R2", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Documentos.Add(new PacienteDocumento { Id = Guid.NewGuid(), NomeDocumento = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p);
        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(() => db.SaveChangesAsync());
        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task EntityMap_Should_cascade_to_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithMaps>()
            .UseSqlServer(ConnectionStringFor(nameof(EntityMap_Should_cascade_to_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithMaps(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "Maps", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Notificacoes.Add(new Notificacao { Id = Guid.NewGuid(), Diagnostico = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.True((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Notificacoes[0].Id)).IsDeleted);
    }

    [Fact]
    public async Task Should_mark_IsDeleted_and_DeletedAt()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_mark_IsDeleted_and_DeletedAt)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var p = new Paciente { Id = Guid.NewGuid(), Nome = "João", Endereco = new PacienteEndereco { Logradouro = "Rua A", Cidade = "C" } };
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();

        var deleted = await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }

    // ── DbContext com assembly scan ─────────────────────────

    [Fact]
    public async Task AssemblyScan_Should_set_null_on_SetNull_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(AssemblyScan_Should_set_null_on_SetNull_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "ASM", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.HistoricoEnderecos.Add(new EnderecoHistorico { Id = Guid.NewGuid(), Logradouro = "Rua X", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.Null((await db.Set<EnderecoHistorico>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.HistoricoEnderecos[0].Id)).PacienteId);
    }

    [Fact]
    public async Task AssemblyScan_Should_throw_Restrict_when_dependents_exist()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(AssemblyScan_Should_throw_Restrict_when_dependents_exist)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "R3", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Documentos.Add(new PacienteDocumento { Id = Guid.NewGuid(), NomeDocumento = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p);
        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(() => db.SaveChangesAsync());
        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task AssemblyScan_Should_cascade_to_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(AssemblyScan_Should_cascade_to_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "M2", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Notificacoes.Add(new Notificacao { Id = Guid.NewGuid(), Diagnostico = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.True((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Notificacoes[0].Id)).IsDeleted);
    }

    

    // ── DbContext com herança indireta (AssemblyScan) ────────

    [Fact]
    public async Task IndirectAssemblyScan_Should_set_null_on_SetNull_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithIndirectAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(IndirectAssemblyScan_Should_set_null_on_SetNull_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithIndirectAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "IndASM", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.HistoricoEnderecos.Add(new EnderecoHistorico { Id = Guid.NewGuid(), Logradouro = "Rua X", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.Null((await db.Set<EnderecoHistorico>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.HistoricoEnderecos[0].Id)).PacienteId);
    }

    [Fact]
    public async Task IndirectAssemblyScan_Should_throw_Restrict_when_dependents_exist()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithIndirectAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(IndirectAssemblyScan_Should_throw_Restrict_when_dependents_exist)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithIndirectAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "IndASR", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Documentos.Add(new PacienteDocumento { Id = Guid.NewGuid(), NomeDocumento = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p);
        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(() => db.SaveChangesAsync());
        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task IndirectAssemblyScan_Should_cascade_to_children()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextWithIndirectAssemblyScan>()
            .UseSqlServer(ConnectionStringFor(nameof(IndirectAssemblyScan_Should_cascade_to_children)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContextWithIndirectAssemblyScan(options);
        await db.Database.EnsureCreatedAsync();

        var pId = Guid.NewGuid();
        var p = new Paciente { Id = pId, Nome = "IndASC", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        p.Notificacoes.Add(new Notificacao { Id = Guid.NewGuid(), Diagnostico = "D", PacienteId = pId });
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.True((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Notificacoes[0].Id)).IsDeleted);
    }

    // ── DbContext com ISoftDeleteEntity via herança (BaseSoftDelete) ──

    [Fact]
    public async Task Inline_Should_soft_delete_entity_with_inherited_ISoftDeleteEntity()
    {
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Inline_Should_soft_delete_entity_with_inherited_ISoftDeleteEntity)))
            .AddInterceptors(InterceptorWithGeneratedHandlers()).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        db.Set<Paciente>().Add(new Paciente { Id = pacienteId, Nome = "Inherited", Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cidade" } });
        await db.SaveChangesAsync();

        var protocoloId = Guid.NewGuid();
        var protocolo = new Protocolo
        {
            Id = protocoloId,
            Numero = "PROT-001",
            PacienteId = pacienteId
        };
        db.Set<Protocolo>().Add(protocolo); await db.SaveChangesAsync();
        db.Set<Protocolo>().Remove(protocolo); await db.SaveChangesAsync();

        var deleted = await db.Set<Protocolo>().IgnoreQueryFilters().FirstAsync(x => x.Id == protocoloId);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }
}
