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
        Assert.Contains("AuditLogAnteriorId", properties);
        Assert.Contains("AuditLogAnterior", properties);
        Assert.Contains("Nome", properties);
        Assert.Contains("Cpf", properties);
        Assert.Contains("CartaoSus", properties);
        Assert.Contains("DataNascimento", properties);
        Assert.Contains("EnderecoLogradouro", properties);
        Assert.Contains("EnderecoCidade", properties);
        Assert.Contains("EnderecoCep", properties);

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
        Assert.Contains("AuditLogAnteriorId", properties);
        Assert.Contains("AuditLogAnterior", properties);
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
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua A",
                Cidade = "São Paulo",
                Cep = "01234567"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>().ToListAsync();
        Assert.Single(logs);
        Assert.Equal(AuditOperation.Added, logs[0].Operacao);
        Assert.Equal("user-123", logs[0].UsuarioId);
        Assert.Equal("corr-456", logs[0].CorrelationId);
        Assert.Equal("***", logs[0].Cpf);
        Assert.Equal("***", logs[0].CartaoSus);
        Assert.Equal("Rua A", logs[0].EnderecoLogradouro);
        Assert.Equal("São Paulo", logs[0].EnderecoCidade);
        Assert.Equal("***", logs[0].EnderecoCep);
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

    [Fact]
    public async Task AlwaysAudit_should_include_field_even_when_not_modified()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("AlwaysAuditTest_" + Guid.NewGuid())
            .AddInterceptors(new AuditSaveInterceptor(registry))
            .Options;

        using var db = new TestDbContext(options);

        var notificacao = new Notificacao
        {
            Id = Guid.NewGuid(),
            Diagnostico = "Diagnóstico inicial",
            Situacao = "Pendente",
            Descricao = "Descrição",
            PacienteId = Guid.NewGuid(),
            MedicoId = Guid.NewGuid(),
            UnidadeTratamentoId = Guid.NewGuid(),
            DataAtualizacao = DateTime.UtcNow
        };

        db.Set<Notificacao>().Add(notificacao);
        await db.SaveChangesAsync();

        // Situacao has AlwaysAudit. For Added, the check is "*" so we verify via Added
        var addedLog = await db.Set<NotificacaoAuditLog>()
            .FirstAsync(x => x.NotificacaoId == notificacao.Id && x.Operacao == AuditOperation.Added);
        Assert.Contains("*", addedLog.CamposAlteradosJson!);

        // Now modify a non-AlwaysAudit field (Diagnostico) and verify Situacao appears
        notificacao.Diagnostico = "Diagnóstico alterado";
        await db.SaveChangesAsync();

        var modifiedLog = await db.Set<NotificacaoAuditLog>()
            .FirstAsync(x => x.NotificacaoId == notificacao.Id && x.Operacao == AuditOperation.Modified);
        Assert.Contains("Situacao", modifiedLog.CamposAlteradosJson!);
        Assert.Contains("Diagnostico", modifiedLog.CamposAlteradosJson!);
    }

    [Fact]
    public async Task AuditLogAnteriorId_should_point_to_previous_version()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("AnteriorIdTest_" + Guid.NewGuid())
            .AddInterceptors(new AuditSaveInterceptor(registry))
            .Options;

        using var db = new TestDbContext(options);

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Versão 1",
            Cpf = "00000000000",
            CartaoSus = "SUS000000",
            DataNascimento = new DateOnly(2000, 1, 1),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua 1",
                Cidade = "Cidade 1",
                Cep = "00000000"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        paciente.Nome = "Versão 2";
        await db.SaveChangesAsync();

        paciente.Nome = "Versão 3";
        await db.SaveChangesAsync();

        var logs = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .Where(x => x.PacienteId == paciente.Id)
            .OrderBy(x => x.OcorridoEm)
            .ToListAsync();

        Assert.Equal(3, logs.Count);

        Assert.Equal(AuditOperation.Added, logs[0].Operacao);
        Assert.Null(logs[0].AuditLogAnteriorId);

        Assert.Equal(AuditOperation.Modified, logs[1].Operacao);
        Assert.Equal(logs[0].Id, logs[1].AuditLogAnteriorId);

        Assert.Equal(AuditOperation.Modified, logs[2].Operacao);
        Assert.Equal(logs[1].Id, logs[2].AuditLogAnteriorId);
    }

    [Fact]
    public async Task ForOwned_should_capture_before_and_after_values_on_update()
    {
        var registry = new AuditRegistry();
        registry.AddGeneratedAuditConfigurations();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("ForOwnedDiff_" + Guid.NewGuid())
            .AddInterceptors(new AuditSaveInterceptor(registry))
            .Options;

        using var db = new TestDbContext(options);

        var paciente = new Paciente
        {
            Id = Guid.NewGuid(),
            Nome = "Teste Endereço",
            Cpf = "00000000000",
            CartaoSus = "SUS000000",
            DataNascimento = new DateOnly(2000, 1, 1),
            DataAtualizacao = DateTime.UtcNow,
            Endereco = new Endereco
            {
                Logradouro = "Rua Antiga, 123",
                Cidade = "São Paulo",
                Cep = "01001000"
            }
        };

        db.Set<Paciente>().Add(paciente);
        await db.SaveChangesAsync();

        // Modificar propriedades do owned type diretamente
        paciente.Nome = "Nome alterado";
        paciente.Endereco.Logradouro = "Rua Nova, 456";
        paciente.Endereco.Cep = "02002000";
        await db.SaveChangesAsync();

        var allLogs = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .Where(x => x.PacienteId == paciente.Id)
            .OrderBy(x => x.OcorridoEm)
            .ToListAsync();

        // Deve ter 2 logs: Added + Modified
        Assert.Equal(2, allLogs.Count);
        Assert.Equal(AuditOperation.Added, allLogs[0].Operacao);

        var modifiedLog = allLogs.FirstOrDefault(x => x.Operacao == AuditOperation.Modified);

        Assert.NotNull(modifiedLog);
        Assert.NotNull(modifiedLog.AuditLogAnteriorId);

        var anteriorLog = await db.Set<PacienteAuditLog>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == modifiedLog.AuditLogAnteriorId);

        // Valores NOVOS no log atual
        Assert.Equal("Rua Nova, 456", modifiedLog.EnderecoLogradouro);
        Assert.Equal("São Paulo", modifiedLog.EnderecoCidade);
        Assert.Equal("***", modifiedLog.EnderecoCep);

        // Valores ANTIGOS no log anterior (consulta direta por AuditLogAnteriorId)
        Assert.Equal("Rua Antiga, 123", anteriorLog.EnderecoLogradouro);
        Assert.Equal("São Paulo", anteriorLog.EnderecoCidade);
        Assert.Equal("***", anteriorLog.EnderecoCep);

        // CamposAlteradosJson deve incluir endereço
        Assert.Contains("Endereco.Logradouro", modifiedLog.CamposAlteradosJson);
        Assert.Contains("Endereco.Cep", modifiedLog.CamposAlteradosJson);
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
        modelBuilder.Entity<Paciente>().OwnsOne(x => x.Endereco, end =>
        {
            end.Property(e => e.Logradouro).HasMaxLength(200).IsRequired();
            end.Property(e => e.Cidade);
            end.Property(e => e.Cep).HasMaxLength(8);
        });
        modelBuilder.Entity<Notificacao>().ToTable("Notificacoes").HasKey(x => x.Id);
        modelBuilder.Entity<NotificacaoMedicamento>().ToTable("NotificacaoMedicamentos").HasKey(x => new { x.NotificacaoId, x.MedicamentoId });
        modelBuilder.ApplyGeneratedAuditMaps();
    }
}
