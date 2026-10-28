using Npgsql;
using Testcontainers.PostgreSql;

namespace TicketFlow.IntegrationTests;

public sealed class PostgresDatabase : IAsyncDisposable
{
    private PostgreSqlContainer? _container;
    private string? _master;
    private string? _name;
    public string ConnectionString { get; private set; } = "";

    public static async Task<PostgresDatabase> CreateAsync()
    {
        var database = new PostgresDatabase();
        var native = Environment.GetEnvironmentVariable("TICKETFLOW_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(native))
        {
            database._container = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
            await database._container.StartAsync();
            database.ConnectionString = database._container.GetConnectionString();
        }
        else
        {
            var connection = new NpgsqlConnectionStringBuilder(native) { Database = "postgres" };
            database._master = connection.ConnectionString;
            database._name = "ticketflow_test_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(database._master);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {database._name}", admin);
            await command.ExecuteNonQueryAsync();
            connection.Database = database._name;
            database.ConnectionString = connection.ConnectionString;
        }
        return database;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
        if (_master is not null && _name is not null && _name.StartsWith("ticketflow_test_", StringComparison.Ordinal))
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(_master);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE {_name} WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}
