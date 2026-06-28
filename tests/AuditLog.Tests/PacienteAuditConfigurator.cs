using AuditLog.Abstractions;

[GenerateAuditLog]
public sealed partial class PacienteAuditConfigurator
    : AuditConfigurator<Paciente>
{
    public PacienteAuditConfigurator()
    {
        For(x => x.Id)
            .Key();

        For(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        For(x => x.Cpf)
            .Sensitive()
            .HasMaxLength(11);

        For(x => x.CartaoSus)
            .Sensitive();

        For(x => x.DataNascimento);

        For(x => x.DataAtualizacao)
            .Ignore();

        ForOwned(x => x.Endereco, o =>
        {
            o.For(x => x.Logradouro).HasMaxLength(200).IsRequired();
            o.For(x => x.Cidade);
            o.For(x => x.Cep).Sensitive().HasMaxLength(8);
        });
    }
}
