namespace AuditLog.Abstractions;

public readonly record struct AuditExecutionContext(
    DateTimeOffset OcorridoEm,
    string? UsuarioId,
    string? CorrelationId);
