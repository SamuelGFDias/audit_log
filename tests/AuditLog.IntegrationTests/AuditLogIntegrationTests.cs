using AuditLog.Abstractions;
using AuditLog.EntityFrameworkCore;
using AuditLog.Generated;
using AuditLog.TestContainers.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLog.IntegrationTests;

public sealed class AuditLogIntegrationTests
{
    private static string ConnectionStringFor(string dbName)
        => MsSqlContainerFixture.GetConnectionString(dbName);

    [Fact]
    public async Task Should_audit_Paciente_insert_in_SQL_Server()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var interceptor = new AuditSaveInterceptor(registry, () =>
            new AuditExecutionContext(
                DateTimeOffset.UtcNow,
                UsuarioId: "integration-test-user",
                CorrelationId: "test-corr-001"));

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_audit_Paciente_insert_in_SQL_Server)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Maria Silva",
            Cpf = "98765432100",
            CartaoSus = "SUS987654",
            DataNascimento = new DateOnly(1985, 5, 20),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua Teste",
                Cidade = "Cidade Teste",
                Cep = "00000000"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .Where(x => x.PacienteId == paciente.Id)
            .OrderBy(x => x.OcorridoEm)
            .ToListAsync();

        Assert.Single(logs);
        Assert.Equal(AuditOperation.Added, logs[0].Operacao);
        Assert.Equal("integration-test-user", logs[0].UsuarioId);
        Assert.Equal("test-corr-001", logs[0].CorrelationId);
        Assert.Equal("***", logs[0].Cpf);
        Assert.Equal("***", logs[0].CartaoSus);
        Assert.Equal(paciente.Nome, logs[0].Nome);
    }

    [Fact]
    public async Task Should_audit_Paciente_update_in_SQL_Server()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var interceptor = new AuditSaveInterceptor(registry, () =>
            new AuditExecutionContext(
                DateTimeOffset.UtcNow,
                UsuarioId: "integration-test-user",
                CorrelationId: "test-corr-002"));

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_audit_Paciente_update_in_SQL_Server)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "João Original",
            Cpf = "11122233344",
            CartaoSus = "SUS111222",
            DataNascimento = new DateOnly(1990, 1, 15),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua Original",
                Cidade = "Cidade Original",
                Cep = "11111111"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        paciente.Nome = "João Atualizado";
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .Where(x => x.PacienteId == paciente.Id)
            .OrderBy(x => x.OcorridoEm)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal(AuditOperation.Added, logs[0].Operacao);
        Assert.Equal(AuditOperation.Modified, logs[1].Operacao);
        Assert.Contains("Nome", logs[1].CamposAlteradosJson ?? "");
    }

    [Fact]
    public async Task Should_generate_CamposAlteradosJson_with_changed_fields()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var interceptor = new AuditSaveInterceptor(registry, () =>
            new AuditExecutionContext(
                DateTimeOffset.UtcNow,
                UsuarioId: "test-user",
                CorrelationId: "test-corr-003"));

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_generate_CamposAlteradosJson_with_changed_fields)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Teste",
            Cpf = "00000000000",
            CartaoSus = "SUS000000",
            DataNascimento = new DateOnly(2000, 1, 1),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua Teste",
                Cidade = "Cidade Teste",
                Cep = "00000000"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        var addedLog = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .FirstAsync(x => x.PacienteId == paciente.Id && x.Operacao == AuditOperation.Added);

        Assert.NotNull(addedLog.CamposAlteradosJson);
        Assert.Contains("*", addedLog.CamposAlteradosJson);

        paciente.Nome = "Teste Atualizado";
        paciente.CartaoSus = "SUS999999";
        await db.SaveChangesAsync();

        var modifiedLog = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .FirstAsync(x => x.PacienteId == paciente.Id && x.Operacao == AuditOperation.Modified);

        Assert.NotNull(modifiedLog.CamposAlteradosJson);
        Assert.Contains("Nome", modifiedLog.CamposAlteradosJson);
        Assert.Contains("CartaoSus", modifiedLog.CamposAlteradosJson);
    }

    [Fact]
    public async Task Should_audit_Paciente_delete_in_SQL_Server()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var interceptor = new AuditSaveInterceptor(registry, () =>
            new AuditExecutionContext(
                DateTimeOffset.UtcNow,
                UsuarioId: "test-user",
                CorrelationId: "test-corr-004"));

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionStringFor(nameof(Should_audit_Paciente_delete_in_SQL_Server)))
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new TestDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Delete Test",
            Cpf = "99988877766",
            CartaoSus = "SUS999888",
            DataNascimento = new DateOnly(1995, 3, 10),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua Delete",
                Cidade = "Cidade Delete",
                Cep = "99999999"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        db.Set<Paciente>().Remove(paciente);
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .Where(x => x.PacienteId == paciente.Id)
            .OrderBy(x => x.OcorridoEm)
            .ToListAsync();

        Assert.Equal(2, logs.Count);
        Assert.Equal(AuditOperation.Added, logs[0].Operacao);
        Assert.Equal(AuditOperation.Deleted, logs[1].Operacao);
    }
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Paciente>().ToTable("Pacientes").HasKey(x => x.Id);
        modelBuilder.Entity<Paciente>().OwnsOne(x => x.Endereco, end =>
        {
            end.Property(e => e.Logradouro).HasMaxLength(200).IsRequired();
            end.Property(e => e.Cidade);
            end.Property(e => e.Cep).HasMaxLength(8);
        });
        modelBuilder.ApplyGeneratedAuditMaps();
    }
}
