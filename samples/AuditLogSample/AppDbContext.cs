using AuditLog.Abstractions;
using AuditLog.EntityFrameworkCore;
using AuditLog.EntityFrameworkCore.SoftDelete;
using AuditLog.Generated;
using Microsoft.EntityFrameworkCore;

[GenerateSoftDelete]
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();
    public DbSet<Contrato> Contratos => Set<Contrato>();
    public DbSet<DocumentoEmpresa> DocumentosEmpresa => Set<DocumentoEmpresa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);


        modelBuilder.ApplyGeneratedAuditMaps();
        modelBuilder.ApplySoftDeleteQueryFilter();
        base.OnModelCreating(modelBuilder);
    }
}