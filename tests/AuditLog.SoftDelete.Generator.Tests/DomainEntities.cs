using AuditLog.EntityFrameworkCore.SoftDelete;
using Microsoft.EntityFrameworkCore;

public sealed class Paciente : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    [AuditLog.EntityFrameworkCore.SoftDelete.DeleteBehavior(CascadeDeleteBehavior.Cascade)]
    public List<Notificacao> Notificacoes { get; set; } = [];
    [AuditLog.EntityFrameworkCore.SoftDelete.DeleteBehavior(CascadeDeleteBehavior.Restrict)]
    public List<PacienteDocumento> Documentos { get; set; } = [];
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

public sealed class PacienteEndereco
{
    public string Logradouro { get; set; } = null!;
    public string Cidade { get; set; } = null!;
}

[GenerateSoftDelete]
public sealed class GeneratedTestDbContext : DbContext
{
    public GeneratedTestDbContext(DbContextOptions<GeneratedTestDbContext> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();

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
        });
        modelBuilder.Entity<Notificacao>(e => { e.ToTable("Notificacoes").HasKey(x => x.Id); e.Property(x => x.Diagnostico).IsRequired(); });
        modelBuilder.Entity<PacienteDocumento>(e => { e.ToTable("PacienteDocumentos").HasKey(x => x.Id); e.Property(x => x.NomeDocumento).HasMaxLength(200); });
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}

[GenerateSoftDelete]
public sealed class GeneratedTestDbContextSetNull : DbContext
{
    public GeneratedTestDbContextSetNull(DbContextOptions<GeneratedTestDbContextSetNull> options) : base(options) { }

    public DbSet<Paciente> Pacientes => Set<Paciente>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<PacienteDocumento> PacienteDocumentos => Set<PacienteDocumento>();

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
            e.HasMany(x => x.Notificacoes).WithOne(x => x.Paciente).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Documentos).WithOne(x => x.Paciente).HasForeignKey(x => x.PacienteId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Notificacao>(e => { e.ToTable("Notificacoes").HasKey(x => x.Id); e.Property(x => x.Diagnostico).IsRequired(); });
        modelBuilder.Entity<PacienteDocumento>(e => { e.ToTable("PacienteDocumentos").HasKey(x => x.Id); e.Property(x => x.NomeDocumento).HasMaxLength(200); });
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}
