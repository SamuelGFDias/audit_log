using AuditLog.Abstractions;
using AuditLog.EntityFrameworkCore;
using AuditLog.EntityFrameworkCore.SoftDelete;
using AuditLog.Generated;
using Microsoft.EntityFrameworkCore;

// ── Setup ───────────────────────────────────────────────────────────

var auditRegistry = new AuditRegistry();
auditRegistry.AddGeneratedAuditConfigurations();

var softDeleteRegistry = new SoftDeleteHandlerRegistry();
softDeleteRegistry.AddGeneratedSoftDeleteHandlers();

var connectionString = "Server=localhost;Database=AuditLogSample;Trusted_Connection=true;TrustServerCertificate=true;";

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(new AuditSaveInterceptor(auditRegistry))
    .AddInterceptors(new SoftDeleteInterceptor(softDeleteRegistry))
    .Options;

await using var db = new AppDbContext(options);
await db.Database.EnsureCreatedAsync();

// ── 1. Inserir empresa com funcionário ─────────────────────────────

var empresaId = Guid.NewGuid();
var empresa = new Empresa
{
    Id = empresaId,
    Nome = "AuditLog Ltda",
    Cnpj = "11222333000181",
    Funcionarios =
    [
        new() { Id = Guid.NewGuid(), Nome = "João", Cargo = "Dev", Salario = 10000, EmpresaId = empresaId }
    ],
    Contratos =
    [
        new() { Id = Guid.NewGuid(), Numero = "CT-001", Valor = 50000, EmpresaId = empresaId }
    ],
    Documentos =
    [
        new() { Id = Guid.NewGuid(), Nome = "Contrato Social", EmpresaId = empresaId }
    ]
};

db.Add(empresa);
await db.SaveChangesAsync();
Console.WriteLine("✅ Empresa criada com funcionário (Cascade), contrato (Restrict) e documento (SetNull)");

// ── 2. Atualizar empresa (gera audit log) ──────────────────────────

var saved = await db.Empresas.FirstAsync(x => x.Id == empresaId);
saved.Nome = "AuditLog S.A.";
await db.SaveChangesAsync();
Console.WriteLine("✅ Empresa atualizada — audit log gerado");

// ── 3. Verificar audit log ─────────────────────────────────────────

var auditLogs = await db.Set<EmpresaAuditLog>().OrderBy(x => x.Id).ToListAsync();
foreach (var log in auditLogs)
{
    Console.WriteLine($"  Audit #{log.Id}: {log.Operacao} em {log.OcorridoEm:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"    Nome: {log.Nome}");
    Console.WriteLine($"    Cnpj: {log.Cnpj}");
    Console.WriteLine($"    CamposAlterados: {log.CamposAlteradosJson}");
}

// ── 4. Tentar deletar com contrato (Restrict → deve lançar) ────────

db.Remove(saved);
try
{
    await db.SaveChangesAsync();
    Console.WriteLine("❌ ERRO: deveria ter lançado RestrictDeleteViolationException");
}
catch (RestrictDeleteViolationException ex)
{
    Console.WriteLine($"✅ Restrict funcionou: {ex.Message}");
}

// ── 5. Remover contrato e deletar empresa ──────────────────────────

var empresaReloaded = await db.Empresas
    .Include(x => x.Contratos)
    .Include(x => x.Documentos)
    .FirstAsync(x => x.Id == empresaId);

db.RemoveRange(empresaReloaded.Contratos);
await db.SaveChangesAsync();
Console.WriteLine("✅ Contratos removidos");

db.Remove(empresaReloaded);
await db.SaveChangesAsync();
Console.WriteLine("✅ Empresa soft-deletada com sucesso");

// ── 6. Verificar soft delete ───────────────────────────────────────

var deletedEmpresa = await db.Empresas.IgnoreQueryFilters()
    .FirstAsync(x => x.Id == empresaId);
Console.WriteLine($"  Empresa IsDeleted={deletedEmpresa.IsDeleted}, DeletedAt={deletedEmpresa.DeletedAt:yyyy-MM-dd HH:mm:ss}");

var funcionarios = await db.Funcionarios.IgnoreQueryFilters()
    .Where(x => x.EmpresaId == empresaId).ToListAsync();
Console.WriteLine($"  Funcionarios (Cascade): {funcionarios.Count} — IsDeleted={funcionarios.All(f => f.IsDeleted)}");

var documentos = await db.DocumentosEmpresa.IgnoreQueryFilters()
    .Where(x => x.EmpresaId == empresaId).ToListAsync();
Console.WriteLine($"  Documentos (SetNull): {documentos.Count} — EmpresaId={(documentos.All(d => d.EmpresaId == null) ? "null ✅" : "NOT null ❌")}");
