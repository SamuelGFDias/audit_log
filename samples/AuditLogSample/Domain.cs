using AuditLog.EntityFrameworkCore.SoftDelete;

public sealed class Empresa : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<Funcionario> Funcionarios { get; set; } = [];
    public List<Contrato> Contratos { get; set; } = [];
    public List<DocumentoEmpresa> Documentos { get; set; } = [];
}

public sealed class Funcionario : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public decimal Salario { get; set; }
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class Contrato : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class DocumentoEmpresa : ISoftDeleteEntity
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
