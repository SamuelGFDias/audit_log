using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

[GenerateAuditLog]
public sealed class EmpresaAuditConfigurator : AuditConfigurator<Empresa>
{
    public EmpresaAuditConfigurator()
    {
        For(x => x.Id).Key();
        For(x => x.Nome).HasMaxLength(200).IsRequired();
        For(x => x.Cnpj).Sensitive().HasMaxLength(14);
    }
}

public sealed class EmpresaEntityMap : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresas").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cnpj).HasMaxLength(14).IsRequired();

        builder.HasMany(x => x.Funcionarios)
            .WithOne(x => x.Empresa)
            .HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Contratos)
            .WithOne(x => x.Empresa)
            .HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Documentos)
            .WithOne(x => x.Empresa)
            .HasForeignKey(x => x.EmpresaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class FuncionarioEntityMap : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> builder)
    {
        builder.ToTable("Funcionarios").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cargo).HasMaxLength(100);
    }
}

public sealed class ContratoEntityMap : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.ToTable("Contratos").HasKey(x => x.Id);
        builder.Property(x => x.Numero).HasMaxLength(50).IsRequired();
    }
}

public sealed class DocumentoEmpresaEntityMap : IEntityTypeConfiguration<DocumentoEmpresa>
{
    public void Configure(EntityTypeBuilder<DocumentoEmpresa> builder)
    {
        builder.ToTable("DocumentosEmpresa").HasKey(x => x.Id);
        builder.Property(x => x.Nome).HasMaxLength(200).IsRequired();
    }
}
