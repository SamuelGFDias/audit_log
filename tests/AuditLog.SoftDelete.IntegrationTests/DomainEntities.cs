using AuditLog.EntityFrameworkCore.SoftDelete;

public sealed class Paciente : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<Notificacao> Notificacoes { get; set; } = [];
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
