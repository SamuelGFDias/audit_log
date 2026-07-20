namespace AuditLog.Tests;

public sealed class Paciente
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Cpf { get; set; } = null!;
    public string CartaoSus { get; set; } = null!;
    public DateOnly DataNascimento { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public Endereco Endereco { get; set; } = null!;
}

public sealed class Endereco
{
    public string Logradouro { get; set; } = null!;
    public string Cidade { get; set; } = null!;
    public string Cep { get; set; } = null!;
}

public sealed class Notificacao
{
    public Guid Id { get; set; }
    public string Diagnostico { get; set; } = null!;
    public string Situacao { get; set; } = null!;
    public string? Descricao { get; set; }
    public string? Observacao { get; set; }
    public Guid PacienteId { get; set; }
    public Guid MedicoId { get; set; }
    public Guid UnidadeTratamentoId { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public List<NotificacaoMedicamento> Medicamentos { get; set; } = [];
}

public sealed class NotificacaoMedicamento
{
    public Guid NotificacaoId { get; set; }
    public Guid MedicamentoId { get; set; }
    public bool MedicamentoUsoContinuo { get; set; }
    public string OrigemMedicamento { get; set; } = null!;
}