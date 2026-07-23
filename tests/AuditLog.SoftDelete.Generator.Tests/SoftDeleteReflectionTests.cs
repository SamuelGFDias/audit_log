using AuditLog.EntityFrameworkCore.SoftDelete;
using AuditLog.TestContainers.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLog.SoftDelete.Generator.Tests;

public sealed class SoftDeleteReflectionTests
{
    private static string ConnectionStringFor(string dbName)
        => MsSqlContainerFixture.GetConnectionString(dbName);

    private SoftDeleteInterceptor Interceptor() => new();

    private TestDbContext CreateDb(string testName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(testName))
            .AddInterceptors(Interceptor()).Options;
        var db = new TestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Should_mark_IsDeleted_and_DeletedAt_on_soft_delete()
    {
        await using var db = CreateDb(nameof(Should_mark_IsDeleted_and_DeletedAt_on_soft_delete));
        var paciente = new Paciente { Id = Guid.NewGuid(), Nome = "João Silva", Endereco = new PacienteEndereco { Logradouro = "Rua A", Cidade = "Cidade B" } };
        db.Set<Paciente>().Add(paciente); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(paciente); await db.SaveChangesAsync();
        var deleted = await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == paciente.Id);
        Assert.True(deleted.IsDeleted); Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public async Task Should_cascade_soft_delete_to_children()
    {
        await using var db = CreateDb(nameof(Should_cascade_soft_delete_to_children));
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
        await using var db = CreateDb(nameof(Should_throw_RestrictDeleteViolationException_when_dependents_exist));
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
        var interceptor = Interceptor();
        var options = new DbContextOptionsBuilder<TestDbContextSetNull>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_set_null_on_referencing_FK_when_SetNull)))
            .AddInterceptors(interceptor).Options;
        await using var db = new TestDbContextSetNull(options);
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
        await using var db = CreateDb(nameof(Should_filter_out_soft_deleted_entities_by_default));
        var p1 = new Paciente { Id = Guid.NewGuid(), Nome = "Ativo", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        var p2 = new Paciente { Id = Guid.NewGuid(), Nome = "Del", Endereco = new PacienteEndereco { Logradouro = "R2", Cidade = "C2" } };
        db.Set<Paciente>().AddRange(p1, p2); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p2); await db.SaveChangesAsync();
        Assert.Single(await db.Set<Paciente>().ToListAsync());
    }

    [Fact]
    public async Task Should_return_all_entities_with_IgnoreQueryFilters()
    {
        await using var db = CreateDb(nameof(Should_return_all_entities_with_IgnoreQueryFilters));
        var p1 = new Paciente { Id = Guid.NewGuid(), Nome = "A", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        var p2 = new Paciente { Id = Guid.NewGuid(), Nome = "D", Endereco = new PacienteEndereco { Logradouro = "R2", Cidade = "C2" } };
        db.Set<Paciente>().AddRange(p1, p2); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p2); await db.SaveChangesAsync();
        Assert.Equal(2, await db.Set<Paciente>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Should_not_throw_when_deleting_entity_without_dependents_Restrict()
    {
        await using var db = CreateDb(nameof(Should_not_throw_when_deleting_entity_without_dependents_Restrict));
        var p = new Paciente { Id = Guid.NewGuid(), Nome = "SD", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.True((await db.Set<Paciente>().IgnoreQueryFilters().FirstAsync(x => x.Id == p.Id)).IsDeleted);
    }

    [Fact]
    public async Task Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity()
    {
        await using var db = CreateDb(nameof(Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity));
        var p = new Paciente { Id = Guid.NewGuid(), Nome = "F", Endereco = new PacienteEndereco { Logradouro = "R", Cidade = "C" } };
        db.Set<Paciente>().Add(p); await db.SaveChangesAsync();
        db.Set<Paciente>().Remove(p); await db.SaveChangesAsync();
        Assert.Equal(1, await db.Set<Paciente>().IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.Set<Paciente>().CountAsync());
    }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Paciente>(e =>
        {
            e.ToTable("Pacientes").HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.OwnsOne(x => x.Endereco, end => { end.Property(x => x.Logradouro).HasMaxLength(200); end.Property(x => x.Cidade).HasMaxLength(200); });
            e.HasMany(x => x.Notificacoes).WithOne(x => x.Paciente!).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documentos).WithOne(x => x.Paciente!).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Notificacao>(e => { e.ToTable("Notificacoes").HasKey(x => x.Id); e.Property(x => x.Diagnostico).IsRequired(); });
        mb.Entity<PacienteDocumento>(e => { e.ToTable("PacienteDocumentos").HasKey(x => x.Id); e.Property(x => x.NomeDocumento).HasMaxLength(200); });
        mb.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(mb);
    }
}

public class TestDbContextSetNull : DbContext
{
    public TestDbContextSetNull(DbContextOptions<TestDbContextSetNull> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Paciente>(e =>
        {
            e.ToTable("Pacientes").HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.OwnsOne(x => x.Endereco, end => { end.Property(x => x.Logradouro).HasMaxLength(200); end.Property(x => x.Cidade).HasMaxLength(200); });
            e.HasMany(x => x.Notificacoes).WithOne(x => x.Paciente!).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Documentos).WithOne(x => x.Paciente!).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
        });
        mb.Entity<Notificacao>(e => { e.ToTable("Notificacoes").HasKey(x => x.Id); e.Property(x => x.Diagnostico).IsRequired(); });
        mb.Entity<PacienteDocumento>(e => { e.ToTable("PacienteDocumentos").HasKey(x => x.Id); e.Property(x => x.NomeDocumento).HasMaxLength(200); });
        mb.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(mb);
    }
}
