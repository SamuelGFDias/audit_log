using AuditLog.EntityFrameworkCore.SoftDelete;
using AuditLog.TestContainers.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xunit;

// ── Tests ─────────────────────────────────────────────────

public sealed class SoftDeleteGeneratorFixesTests
{
    private static SoftDeleteInterceptor InterceptorWithGeneratedHandlers()
    {
        var registry = new SoftDeleteHandlerRegistry();
        registry.AddGeneratedSoftDeleteHandlers();
        return new SoftDeleteInterceptor(registry);
    }

    private static FixTestDbContext CreateDb(string testName)
    {
        var options = new DbContextOptionsBuilder<FixTestDbContext>()
            .UseSqlServer(MsSqlContainerFixture.GetConnectionString(testName))
            .AddInterceptors(InterceptorWithGeneratedHandlers())
            .Options;
        var db = new FixTestDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task JoinTable_cascade_only_correct_FK()
    {
        await using var db = CreateDb(nameof(JoinTable_cascade_only_correct_FK));
        var usuarioId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nome = "Test" };
        var area = new AreaTecnica { Id = areaId, Nome = "Area" };
        db.Usuarios.Add(usuario);
        db.AreaTecnicas.Add(area);
        await db.SaveChangesAsync();
        db.UsuarioAreaTecnicas.Add(new UsuarioAreaTecnica
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            AreaTecnicaId = areaId
        });
        await db.SaveChangesAsync();
        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync();
        var uat = await db.UsuarioAreaTecnicas.IgnoreQueryFilters()
            .FirstAsync(x => x.UsuarioId == usuarioId);
        Assert.True(uat.IsDeleted);
        var areaReloaded = await db.AreaTecnicas.IgnoreQueryFilters()
            .FirstAsync(x => x.Id == areaId);
        Assert.False(areaReloaded.IsDeleted);
    }

    [Fact]
    public async Task JoinTable_restrict_when_AreaTecnica_has_usuarios()
    {
        await using var db = CreateDb(nameof(JoinTable_restrict_when_AreaTecnica_has_usuarios));
        var usuarioId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nome = "Test" };
        var area = new AreaTecnica { Id = areaId, Nome = "Area" };
        db.Usuarios.Add(usuario);
        db.AreaTecnicas.Add(area);
        await db.SaveChangesAsync();
        db.UsuarioAreaTecnicas.Add(new UsuarioAreaTecnica
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            AreaTecnicaId = areaId
        });
        await db.SaveChangesAsync();
        db.Entry(area).State = EntityState.Deleted;
        var ex = await Assert.ThrowsAsync<RestrictDeleteViolationException>(
            () => db.SaveChangesAsync());
        Assert.Contains("AreaTecnica", ex.EntityName);
    }

    [Fact]
    public async Task NonSoftDelete_dependent_not_physically_deleted()
    {
        await using var db = CreateDb(nameof(NonSoftDelete_dependent_not_physically_deleted));
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nome = "Test" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        db.Documentos.Add(new Documento { Id = Guid.NewGuid(), Nome = "Doc", UsuarioId = usuarioId });
        await db.SaveChangesAsync();
        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync();
        Assert.True((await db.Usuarios.IgnoreQueryFilters().FirstAsync(x => x.Id == usuarioId)).IsDeleted);
        Assert.Equal(1, await db.Documentos.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Simple_soft_delete_marks_IsDeleted_and_DeletedAt()
    {
        await using var db = CreateDb(nameof(Simple_soft_delete_marks_IsDeleted_and_DeletedAt));
        var usuario = new Usuario { Id = Guid.NewGuid(), Nome = "Simple" };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync();
        var deleted = await db.Usuarios.IgnoreQueryFilters().FirstAsync(x => x.Id == usuario.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
    }

    [Fact]
    public async Task Cascade_to_join_table_soft_deletes_dependents()
    {
        await using var db = CreateDb(nameof(Cascade_to_join_table_soft_deletes_dependents));
        var usuarioId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nome = "Test" };
        var area = new AreaTecnica { Id = areaId, Nome = "Area" };
        db.Usuarios.Add(usuario);
        db.AreaTecnicas.Add(area);
        await db.SaveChangesAsync();
        db.UsuarioAreaTecnicas.Add(new UsuarioAreaTecnica
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            AreaTecnicaId = areaId
        });
        await db.SaveChangesAsync();
        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync();
        var uat = await db.UsuarioAreaTecnicas.IgnoreQueryFilters()
            .FirstAsync(x => x.UsuarioId == usuarioId);
        Assert.True(uat.IsDeleted);
        Assert.NotNull(uat.DeletedAt);
    }
}
