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

    [Fact]
    public async Task Should_mark_IsDeleted_and_DeletedAt_on_soft_delete()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_mark_IsDeleted_and_DeletedAt_on_soft_delete)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente { Id = Guid.NewGuid(), Nome = "João Silva", Endereco = new PacienteEndereco { Logradouro = "Rua A", Cidade = "Cidade B" } };
        db.Set<Paciente>().Add(paciente); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(paciente); await db.SaveChangesAsync();

        var deleted = await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == paciente.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public async Task Should_cascade_soft_delete_to_children()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_cascade_soft_delete_to_children)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente { Id = pacienteId, Nome = "Maria", Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" } };
        var notif = new Notificacao { Id = Guid.NewGuid(), Diagnostico = "D", PacienteId = pacienteId, Paciente = paciente };
        paciente.Notificacoes.Add(notif);
        db.Set<Paciente>().Add(paciente); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(paciente); await db.SaveChangesAsync();

        Assert.True((await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == pacienteId)).IsDeleted);
        Assert.True((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == notif.Id)).IsDeleted);
    }

    [Fact]
    public async Task Should_throw_RestrictDeleteViolationException_when_dependents_exist()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_throw_RestrictDeleteViolationException_when_dependents_exist)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente { Id = pacienteId, Nome = "R", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        paciente.Documentos.Add(new PacienteDocumento { Id = Guid.NewGuid(), NomeDocumento = "D", PacienteId = pacienteId });
        db.Set<Paciente>().Add(paciente); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(paciente);

        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(() => db.SaveChangesAsync());
        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task Should_set_null_on_referencing_FK_when_SetNull()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContextSetNull>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_set_null_on_referencing_FK_when_SetNull)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContextSetNull(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente { Id = pacienteId, Nome = "SN", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        paciente.Notificacoes.Add(new Notificacao { Id = Guid.NewGuid(), Diagnostico = "T", PacienteId = pacienteId });
        db.Set<Paciente>().Add(paciente); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(paciente); await db.SaveChangesAsync();

        Assert.Null((await db.Set<Notificacao>().IgnoreQueryFilters().FirstAsync(x => x.Id == paciente.Notificacoes[0].Id)).PacienteId);
    }

    [Fact]
    public async Task Should_filter_out_soft_deleted_entities_by_default()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_filter_out_soft_deleted_entities_by_default)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var p1 = new Paciente { Id = Guid.NewGuid(), Nome = "Ativo", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        var p2 = new Paciente { Id = Guid.NewGuid(), Nome = "Del", Endereco = new PacienteEndereco { Logradouro = "R2", Cidade = "C2" } };
        db.Set<Paciente>().AddRange(p1, p2); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p2); await db.SaveChangesAsync();

        Assert.Single(await db.Set<Paciente>().ToListAsync());
    }

    [Fact]
    public async Task Should_return_all_entities_with_IgnoreQueryFilters()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_return_all_entities_with_IgnoreQueryFilters)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var p1 = new Paciente { Id = Guid.NewGuid(), Nome = "A", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        var p2 = new Paciente { Id = Guid.NewGuid(), Nome = "D", Endereco = new PacienteEndereco { Logradouro = "R2", Cidade = "C2" } };
        db.Set<Paciente>().AddRange(p1, p2); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p2); await db.SaveChangesAsync();

        Assert.Equal(2, await db.Set<Paciente>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Should_not_throw_when_deleting_entity_without_dependents_Restrict()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_not_throw_when_deleting_entity_without_dependents_Restrict)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var p = new Paciente { Id = Guid.NewGuid(), Nome = "SD", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();

        Assert.True((await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Id)).IsDeleted);
    }

    [Fact]
    public async Task Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity()
    {
        var interceptor = InterceptorWithGeneratedHandlers();
        var options = new DbContextOptionsBuilder<GeneratedTestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity)))
            .AddInterceptors(interceptor).Options;
        await using var db = new GeneratedTestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var p = new Paciente { Id = Guid.NewGuid(), Nome = "F", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();

        Assert.Equal(1, await db.Set<Paciente>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Set<Paciente>().CountAsync());
    }
}
