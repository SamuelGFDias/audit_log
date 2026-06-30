using AuditLog.EntityFrameworkCore.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace AuditLog.SoftDelete.Generator.Tests;

public sealed class SoftDeleteGeneratedTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.StopAsync();

    private string ConnectionStringFor(string dbName)
        => $"{_container.GetConnectionString()};Initial Catalog={dbName};";

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
}
