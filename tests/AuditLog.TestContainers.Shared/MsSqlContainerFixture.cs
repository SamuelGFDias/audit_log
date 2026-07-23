using Testcontainers.MsSql;

namespace AuditLog.TestContainers.Shared;

public static class MsSqlContainerFixture
{
    private static readonly Lazy<MsSqlContainer> _container = new(() =>
    {
        var container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        container.StartAsync().GetAwaiter().GetResult();

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            container.StopAsync().GetAwaiter().GetResult();

        return container;
    });

    public static string GetConnectionString(string dbName)
        => $"{_container.Value.GetConnectionString()};Initial Catalog={dbName};";
}
