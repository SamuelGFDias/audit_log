using AuditLog.Abstractions;
using AuditLog.Tests;

[GenerateAuditLog]
public sealed partial class TestEntityNullableOwnedAuditConfigurator
    : AuditConfigurator<TestEntityNullableOwned>
{
    public TestEntityNullableOwnedAuditConfigurator()
    {
        For(x => x.Id).Key();

        For(x => x.Nome);

        ForOwned(x => x.EmailResponsavelTecnico, o =>
        {
            o.For(e => e.Value);
        });
    }
}
