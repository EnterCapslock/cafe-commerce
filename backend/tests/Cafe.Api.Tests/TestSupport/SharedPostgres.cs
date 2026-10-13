using Npgsql;
using Testcontainers.PostgreSql;

namespace Cafe.Api.Tests.TestSupport;

/// <summary>One Postgres container for the whole test run, with a fresh database for each caller.</summary>
public static class SharedPostgres
{
    private static readonly Lazy<PostgreSqlContainer> Container = new(() =>
    {
        try
        {
            var container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            Task.Run(() => container.StartAsync()).GetAwaiter().GetResult();
            return container; // the test run's resource reaper removes it when the process exits
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Could not start the Postgres test container. Is Docker running?", ex);
        }
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string CreateDatabase()
    {
        var container = Container.Value;
        var name = $"cafe_{Guid.NewGuid():N}";
        var builder = new NpgsqlConnectionStringBuilder(container.GetConnectionString());

        using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            command.ExecuteNonQuery();
        }

        builder.Database = name;
        return builder.ConnectionString;
    }
}