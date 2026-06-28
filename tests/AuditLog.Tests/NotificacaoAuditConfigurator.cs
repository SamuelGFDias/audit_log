using AuditLog.Abstractions;

[GenerateAuditLog]
public sealed partial class NotificacaoAuditConfigurator
    : AuditConfigurator<Notificacao>
{
    public NotificacaoAuditConfigurator()
    {
        For(x => x.Id)
            .Key();

        For(x => x.Diagnostico);

        For(x => x.Situacao)
            .AlwaysAudit();

        For(x => x.Descricao);

        For(x => x.Observacao);

        For(x => x.PacienteId);

        For(x => x.MedicoId);

        For(x => x.UnidadeTratamentoId);

        For(x => x.DataAtualizacao)
            .Ignore();

        ForEach(x => x.Medicamentos)
            .ParentKey(x => x.NotificacaoId)
            .Key(x => x.MedicamentoId)
            .Configure(item =>
            {
                item.For(x => x.MedicamentoId);
                item.For(x => x.MedicamentoUsoContinuo);
                item.For(x => x.OrigemMedicamento);
            });
    }
}
