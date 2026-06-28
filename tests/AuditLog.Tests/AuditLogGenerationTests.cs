using AuditLog.Abstractions;
using AuditLog.EntityFrameworkCore;
using AuditLog.Generated;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLog.Tests;

public sealed class AuditLogGenerationTests
{
    [Fact]
    public void PacienteAuditLog_type_should_exist()
    {
        var type = typeof(PacienteAuditLog);
        Assert.NotNull(type);
        Assert.True(type.IsClass);
    }

    [Fact]
    public void PacienteAuditLogEntityMap_type_should_exist()
    {
        var type = typeof(PacienteAuditLogEntityMap);
        Assert.NotNull(type);
    }

    [Fact]
    public void PacienteAuditLogDescriptor_type_should_exist()
    {
        var type = typeof(PacienteAuditLogDescriptor);
        Assert.NotNull(type);
    }

    [Fact]
    public void PacienteAuditLog_should_have_expected_properties()
    {
        var properties = typeof(PacienteAuditLog).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("Id", properties);
        Assert.Contains("PacienteId", properties);
        Assert.Contains("Operacao", properties);
        Assert.Contains("OcorridoEm", properties);
        Assert.Contains("UsuarioId", properties);
        Assert.Contains("CorrelationId", properties);
        Assert.Contains("CamposAlteradosJson", properties);
        Assert.Contains("Nome", properties);
        Assert.Contains("Cpf", properties);
        Assert.Contains("CartaoSus", properties);
        Assert.Contains("DataNascimento", properties);

        Assert.DoesNotContain("DataAtualizacao", properties);
    }

    [Fact]
    public void NotificacaoAuditLog_type_should_exist()
    {
        var type = typeof(NotificacaoAuditLog);
        Assert.NotNull(type);
    }

    [Fact]
    public void NotificacaoMedicamentoAuditLog_type_should_exist()
    {
        var type = typeof(NotificacaoMedicamentoAuditLog);
        Assert.NotNull(type);
    }

    [Fact]
    public void NotificacaoMedicamento_should_have_parent_and_child_keys()
    {
        var properties = typeof(NotificacaoMedicamentoAuditLog).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("NotificacaoId", properties);
        Assert.Contains("MedicamentoId", properties);
    }

    [Fact]
    public void Registry_extension_method_should_exist()
    {
        var method = typeof(GeneratedAuditRegistryExtensions)
            .GetMethod("AddGeneratedAuditConfigurations");

        Assert.NotNull(method);
    }

    [Fact]
    public void ModelBuilder_extension_method_should_exist()
    {
        var method = typeof(GeneratedAuditModelBuilderExtensions)
            .GetMethod("ApplyGeneratedAuditMaps");

        Assert.NotNull(method);
    }

    [Fact]
    public void Registry_should_register_all_audit_types()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var all = registry.GetAll();

        Assert.Contains(all, x => x.Entity == typeof(Paciente) && x.AuditLog == typeof(PacienteAuditLog));
        Assert.Contains(all, x => x.Entity == typeof(Notificacao) && x.AuditLog == typeof(NotificacaoAuditLog));
        Assert.Contains(all, x => x.Entity == typeof(NotificacaoMedicamento) && x.AuditLog == typeof(NotificacaoMedicamentoAuditLog));
    }

    [Fact]
    public void PacienteAuditLogDescriptor_should_create_log_from_entry()
    {
        var descriptor = PacienteAuditLogDescriptor.Instance;
        Assert.NotNull(descriptor);
        Assert.IsAssignableFrom<IAuditDescriptor<Paciente, PacienteAuditLog>>(descriptor);
    }

    [Fact]
    public void EntityAuditHistory_type_should_work()
    {
        var history = new EntityAuditHistory<Paciente, PacienteAuditLog>
        {
            Entity = new Paciente { Id = Guid.NewGuid(), Nome = "Test" },
            Logs = []
        };

        Assert.NotNull(history.Entity);
        Assert.Empty(history.Logs);
    }

    [Fact]
    public async Task Interceptor_should_create_audit_logs_on_save()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("AuditTest_" + Guid.NewGuid())
            .AddInterceptors(new AuditSaveInterceptor(registry, () =>
                new AuditExecutionContext(
                    DateTimeOffset.UtcNow,
                    UsuarioId: "user-123",
                    CorrelationId: "corr-456")))
            .Options;

        using var db = new TestDbContext(options);

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "João Silva",
            Cpf = "12345678901",
            CartaoSus = "SUS123456",
            DataNascimento = new DateOnly(1990, 1, 1),
            DataAtualizacao = DateTime.UtcNow
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>().ToListAsync();
        Assert.Single(logs);
        Assert.Equal("Added", logs[0].Operacao);
        Assert.Equal("user-123", logs[0].UsuarioId);
        Assert.Equal("corr-456", logs[0].CorrelationId);
        Assert.Equal("***", logs[0].Cpf);
        Assert.Equal("***", logs[0].CartaoSus);
    }

    [Fact]
    public async Task ModelBuilder_extension_should_apply_maps()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("AuditModelTest_" + Guid.NewGuid())
            .AddInterceptors(new AuditSaveInterceptor(registry))
            .Options;

        using var db = new TestDbContext(options);

        Assert.NotNull(db.Model.FindEntityType(typeof(PacienteAuditLog)));
        Assert.NotNull(db.Model.FindEntityType(typeof(NotificacaoAuditLog)));
        Assert.NotNull(db.Model.FindEntityType(typeof(NotificacaoMedicamentoAuditLog)));
    }
}

public sealed class TestDbContext : DbContext
{
    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<NotificacaoMedicamento> NotificacaoMedicamentos => Set<NotificacaoMedicamento>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Paciente>().ToTable("Pacientes").HasKey(x => x.Id);
        modelBuilder.Entity<Notificacao>().ToTable("Notificacoes").HasKey(x => x.Id);
        modelBuilder.Entity<NotificacaoMedicamento>().ToTable("NotificacaoMedicamentos").HasKey(x => new { x.NotificacaoId, x.MedicamentoId });
        modelBuilder.ApplyGeneratedAuditMaps();
    }
}
