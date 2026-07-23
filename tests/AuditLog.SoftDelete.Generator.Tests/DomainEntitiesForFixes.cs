using AuditLog.EntityFrameworkCore.SoftDelete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class Usuario : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<UsuarioAreaTecnica> AreaTecnicas { get; set; } = [];
}

public sealed class AreaTecnica : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<UsuarioAreaTecnica> Usuarios { get; set; } = [];
}

public sealed class UsuarioAreaTecnica : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public Guid? AreaTecnicaId { get; set; }
    public AreaTecnica? AreaTecnica { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class Documento
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
}

public sealed class UsuarioMap : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.HasMany(x => x.AreaTecnicas)
            .WithOne(x => x.Usuario)
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AreaTecnicaMap : IEntityTypeConfiguration<AreaTecnica>
{
    public void Configure(EntityTypeBuilder<AreaTecnica> builder)
    {
        builder.ToTable("AreaTecnicas").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.HasMany(x => x.Usuarios)
            .WithOne(x => x.AreaTecnica)
            .HasForeignKey(x => x.AreaTecnicaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UsuarioAreaTecnicaMap : IEntityTypeConfiguration<UsuarioAreaTecnica>
{
    public void Configure(EntityTypeBuilder<UsuarioAreaTecnica> builder)
    {
        builder.ToTable("UsuarioAreaTecnicas").HasKey(x => x.Id);
    }
}

public sealed class DocumentoMap : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("Documentos").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.UsuarioId);
    }
}

[GenerateSoftDelete]
public sealed class FixTestDbContext : DbContext
{
    public FixTestDbContext(DbContextOptions<FixTestDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AreaTecnica> AreaTecnicas => Set<AreaTecnica>();
    public DbSet<UsuarioAreaTecnica> UsuarioAreaTecnicas => Set<UsuarioAreaTecnica>();
    public DbSet<Documento> Documentos => Set<Documento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FixTestDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}
