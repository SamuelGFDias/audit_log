using AuditLog.EntityFrameworkCore.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class Paciente : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<Notificacao> Notificacoes { get; set; } = [];
    public List<PacienteDocumento> Documentos { get; set; } = [];
    public List<EnderecoHistorico> HistoricoEnderecos { get; set; } = [];
    public PacienteEndereco Endereco { get; set; } = null!;
}

public sealed class Notificacao : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Diagnostico { get; set; } = null!;
    public Guid? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class PacienteDocumento : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string NomeDocumento { get; set; } = null!;
    public Guid? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class EnderecoHistorico : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Logradouro { get; set; } = null!;
    public Guid? PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class PacienteEndereco
{
    public string Logradouro { get; set; } = null!;
    public string Cidade { get; set; } = null!;
}

// ── Herança indireta de ISoftDeleteEntity ────────────────────

public abstract class BaseSoftDelete : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class Protocolo : BaseSoftDelete
{
    public string Numero { get; set; } = null!;
    public Guid PacienteId { get; set; }
    public Paciente? Paciente { get; set; }
}

public sealed class ProtocoloHistorico : BaseSoftDelete
{
    public string Descricao { get; set; } = null!;
    public Guid? ProtocoloId { get; set; }
    public Protocolo? Protocolo { get; set; }
}

// ── Entity Maps ─────────────────────────────────────────

public sealed class PacienteEntityMap : IEntityTypeConfiguration<Paciente>
{
    public void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("Pacientes").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();

        builder.OwnsOne(x => x.Endereco, end =>
        {
            end.Property(x => x.Logradouro).HasMaxLength(200);
            end.Property(x => x.Cidade).HasMaxLength(200);
        });

        builder.HasMany(x => x.Notificacoes)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documentos)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.HistoricoEnderecos)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class NotificacaoEntityMap : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("Notificacoes").HasKey(x => x.Id);
        builder.Property(x => x.Diagnostico).IsRequired();
    }
}

public sealed class PacienteDocumentoEntityMap : IEntityTypeConfiguration<PacienteDocumento>
{
    public void Configure(EntityTypeBuilder<PacienteDocumento> builder)
    {
        builder.ToTable("PacienteDocumentos").HasKey(x => x.Id);
        builder.Property(x => x.NomeDocumento).HasMaxLength(200);
    }
}

public sealed class EnderecoHistoricoEntityMap : IEntityTypeConfiguration<EnderecoHistorico>
{
    public void Configure(EntityTypeBuilder<EnderecoHistorico> builder)
    {
        builder.ToTable("EnderecoHistoricos").HasKey(x => x.Id);
        builder.Property(x => x.Logradouro).HasMaxLength(200);
    }
}

// ── Indirect Entity Map inheritance ──────────────────────────

public abstract class AuditEntityMap<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : class
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder) { }
}

public sealed class PacienteIndirectEntityMap : AuditEntityMap<Paciente>
{
    public override void Configure(EntityTypeBuilder<Paciente> builder)
    {
        builder.ToTable("Pacientes").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.OwnsOne(x => x.Endereco, end =>
        {
            end.Property(x => x.Logradouro).HasMaxLength(200);
            end.Property(x => x.Cidade).HasMaxLength(200);
        });
        builder.HasMany(x => x.Notificacoes)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documentos)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.HistoricoEnderecos)
            .WithOne(x => x.Paciente)
            .HasForeignKey(x => x.PacienteId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class NotificacaoIndirectEntityMap : AuditEntityMap<Notificacao>
{
    public override void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("Notificacoes").HasKey(x => x.Id);
        builder.Property(x => x.Diagnostico).IsRequired();
    }
}

public sealed class PacienteDocumentoIndirectEntityMap : AuditEntityMap<PacienteDocumento>
{
    public override void Configure(EntityTypeBuilder<PacienteDocumento> builder)
    {
        builder.ToTable("PacienteDocumentos").HasKey(x => x.Id);
        builder.Property(x => x.NomeDocumento).HasMaxLength(200);
    }
}

public sealed class EnderecoHistoricoIndirectEntityMap : AuditEntityMap<EnderecoHistorico>
{
    public override void Configure(EntityTypeBuilder<EnderecoHistorico> builder)
    {
        builder.ToTable("EnderecoHistoricos").HasKey(x => x.Id);
        builder.Property(x => x.Logradouro).HasMaxLength(200);
    }
}


// ── DbContext com ApplyConfigurationsFromAssembly com herança indireta ──

[GenerateSoftDelete]
public sealed class GeneratedTestDbContextWithIndirectAssemblyScan : DbContext
{
    public GeneratedTestDbContextWithIndirectAssemblyScan(DbContextOptions<GeneratedTestDbContextWithIndirectAssemblyScan> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();
    public DbSet<EnderecoHistorico> EnderecoHistoricos => Set<EnderecoHistorico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PacienteIndirectEntityMap).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}

// ── DbContext com configuração inline ────────────────────────────────

[GenerateSoftDelete]
public sealed class GeneratedTestDbContext : DbContext
{
    public GeneratedTestDbContext(DbContextOptions<GeneratedTestDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();
    public DbSet<EnderecoHistorico> EnderecoHistoricos => Set<EnderecoHistorico>();
    public DbSet<Protocolo> Protocolos => Set<Protocolo>();

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
            e.HasMany(x => x.Notificacoes).WithOne(x => x.Paciente).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documentos).WithOne(x => x.Paciente).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.HistoricoEnderecos).WithOne(x => x.Paciente).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<Notificacao>(e => { e.ToTable("Notificacoes").HasKey(x => x.Id); e.Property(x => x.Diagnostico).IsRequired(); });
        modelBuilder.Entity<PacienteDocumento>(e => { e.ToTable("PacienteDocumentos").HasKey(x => x.Id); e.Property(x => x.NomeDocumento).HasMaxLength(200); });
        modelBuilder.Entity<EnderecoHistorico>(e => { e.ToTable("EnderecoHistoricos").HasKey(x => x.Id); e.Property(x => x.Logradouro).HasMaxLength(200); });
        modelBuilder.Entity<Protocolo>(e =>
        {
            e.ToTable("Protocolos").HasKey(x => x.Id);
            e.Property(x => x.Numero).HasMaxLength(50).IsRequired();
        });
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}

// ── DbContext com ApplyConfiguration ─────────────────────────────────

[GenerateSoftDelete]
public sealed class GeneratedTestDbContextWithMaps : DbContext
{
    public GeneratedTestDbContextWithMaps(DbContextOptions<GeneratedTestDbContextWithMaps> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();
    public DbSet<EnderecoHistorico> EnderecoHistoricos => Set<EnderecoHistorico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PacienteEntityMap());
        modelBuilder.ApplyConfiguration(new NotificacaoEntityMap());
        modelBuilder.ApplyConfiguration(new PacienteDocumentoEntityMap());
        modelBuilder.ApplyConfiguration(new EnderecoHistoricoEntityMap());
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}

// ── DbContext com ApplyConfigurationsFromAssembly ────────────────

[GenerateSoftDelete]
public sealed class GeneratedTestDbContextWithAssemblyScan : DbContext
{
    public GeneratedTestDbContextWithAssemblyScan(DbContextOptions<GeneratedTestDbContextWithAssemblyScan> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();
    public DbSet<EnderecoHistorico> EnderecoHistoricos => Set<EnderecoHistorico>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PacienteEntityMap).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}
