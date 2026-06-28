using AuditLog.EntityFrameworkCore.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace AuditLog.SoftDelete.IntegrationTests;

public sealed class SoftDeleteIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }

    private string ConnectionStringFor(string dbName)
    {
        var connStr = _container.GetConnectionString();
        return $"{connStr};Initial Catalog={dbName};";
    }

    [Fact]
    public async Task Should_mark_IsDeleted_and_DeletedAt_on_soft_delete()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_mark_IsDeleted_and_DeletedAt_on_soft_delete)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "João Silva",
            Endereco = new PacienteEndereco
            {
                Logradouro = "Rua A",
                Cidade = "Cidade B"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var deleted = await db.Set<Paciente>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == paciente.Id);

        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public async Task Should_cascade_soft_delete_to_children()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_cascade_soft_delete_to_children)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nome = "Maria",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };

        var notif = new Notificacao
        {
            Id = Guid.NewGuid(),
            Diagnostico = "Diagnóstico A",
            PacienteId = pacienteId,
            Paciente = paciente
        };
        paciente.Notificacoes.Add(notif);

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var deletedPaciente = await db.Set<Paciente>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == pacienteId);

        Assert.True(deletedPaciente.IsDeleted);
        Assert.NotNull(deletedPaciente.DeletedAt);

        var deletedNotif = await db.Set<Notificacao>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == notif.Id);

        Assert.True(deletedNotif.IsDeleted);
        Assert.NotNull(deletedNotif.DeletedAt);
    }

    [Fact]
    public async Task Should_throw_RestrictDeleteViolationException_when_dependents_exist()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_throw_RestrictDeleteViolationException_when_dependents_exist)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nome = "Restrict Test",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };

        var documento = new PacienteDocumento
        {
            Id = Guid.NewGuid(),
            NomeDocumento = "Doc.pdf",
            PacienteId = pacienteId
        };
        paciente.Documentos.Add(documento);

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);

        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("Paciente", ex.EntityName);
    }

    [Fact]
    public async Task Should_set_null_on_referencing_FK_when_SetNull()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContextSetNull>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_set_null_on_referencing_FK_when_SetNull)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContextSetNull(options);
        await db.Database.EnsureCreatedAsync();

        var pacienteId = Guid.NewGuid();
        var paciente = new Paciente
        {
            Id = pacienteId,
            Nome = "SetNull Test",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };

        var notif = new Notificacao
        {
            Id = Guid.NewGuid(),
            Diagnostico = "Teste",
            PacienteId = pacienteId
        };
        paciente.Notificacoes.Add(notif);

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var deletedPaciente = await db.Set<Paciente>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == pacienteId);

        Assert.True(deletedPaciente.IsDeleted);

        var updatedNotif = await db.Set<Notificacao>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == notif.Id);

        Assert.Null(updatedNotif.PacienteId);
    }

    [Fact]
    public async Task Should_filter_out_soft_deleted_entities_by_default()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_filter_out_soft_deleted_entities_by_default)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente1 = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Ativo",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };
        var paciente2 = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Deletado",
            Endereco = new PacienteEndereco { Logradouro = "Rua2", Cidade = "Cid2" }
        };

        db.Set<Paciente>().AddRange(paciente1, paciente2);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente2);
        await db.SaveChangesAsync();

        var ativos = await db.Set<Paciente>().ToListAsync();
        Assert.Single(ativos);
        Assert.Equal("Ativo", ativos[0].Nome);
    }

    [Fact]
    public async Task Should_return_all_entities_with_IgnoreQueryFilters()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_return_all_entities_with_IgnoreQueryFilters)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente1 = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Ativo",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };
        var paciente2 = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Deletado",
            Endereco = new PacienteEndereco { Logradouro = "Rua2", Cidade = "Cid2" }
        };

        db.Set<Paciente>().AddRange(paciente1, paciente2);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente2);
        await db.SaveChangesAsync();

        var todos = await db.Set<Paciente>()
            .IgnoreQueryFilters()
            .ToListAsync();

        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task Should_not_throw_when_deleting_entity_without_dependents_Restrict()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_not_throw_when_deleting_entity_without_dependents_Restrict)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Sem Dependentes",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var deleted = await db.Set<Paciente>()
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == paciente.Id);

        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity()
    {
        var interceptor = new SoftDeleteInterceptor();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_convert_physical_delete_to_soft_delete_for_ISoftDeleteEntity)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Físico",
            Endereco = new PacienteEndereco { Logradouro = "Rua", Cidade = "Cid" }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var countAfterDelete = await db.Set<Paciente>().IgnoreQueryFilters().CountAsync();
        Assert.Equal(1, countAfterDelete);

        var activeCount = await db.Set<Paciente>().CountAsync();
        Assert.Equal(0, activeCount);
    }
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Paciente>(e =>
        {
            e.ToTable("Pacientes").HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.OwnsOne(x => x.Endereco, end =>
            {
                end.Property(x => x.Logradouro).HasMaxLength(200);
                end.Property(x => x.Cidade).HasMaxLength(200);
            });
            e.HasMany(x => x.Notificacoes)
                .WithOne(x => x.Paciente)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documentos)
                .WithOne(x => x.Paciente)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("Notificacoes").HasKey(x => x.Id);
            e.Property(x => x.Diagnostico).IsRequired();
        });

        modelBuilder.Entity<PacienteDocumento>(e =>
        {
            e.ToTable("PacienteDocumentos").HasKey(x => x.Id);
            e.Property(x => x.NomeDocumento).HasMaxLength(200);
        });

        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}

public class TestDbContextSetNull : DbContext
{
    public TestDbContextSetNull(DbContextOptions<TestDbContextSetNull> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Paciente>(e =>
        {
            e.ToTable("Pacientes").HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.OwnsOne(x => x.Endereco, end =>
            {
                end.Property(x => x.Logradouro).HasMaxLength(200);
                end.Property(x => x.Cidade).HasMaxLength(200);
            });
            e.HasMany(x => x.Notificacoes)
                .WithOne(x => x.Paciente)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Documentos)
                .WithOne(x => x.Paciente)
                .HasForeignKey(x => x.PacienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("Notificacoes").HasKey(x => x.Id);
            e.Property(x => x.Diagnostico).IsRequired();
        });

        modelBuilder.Entity<PacienteDocumento>(e =>
        {
            e.ToTable("PacienteDocumentos").HasKey(x => x.Id);
            e.Property(x => x.NomeDocumento).HasMaxLength(200);
        });

        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}
