using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Draw.it.Server.Repositories.Postgres;

public class PostgresOptions
{
    public required string ConnectionString { get; init; }
}

public interface IPostgresConnectionFactory
{
    IDbConnection CreateConnection();
}

public class PostgresConnectionFactory : IPostgresConnectionFactory
{
    private readonly ILogger<PostgresConnectionFactory> _logger;
    private readonly PostgresOptions _options;
    private bool _initialized;
    private readonly object _initializationLock = new();

    public PostgresConnectionFactory(ILogger<PostgresConnectionFactory> logger, PostgresOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public IDbConnection CreateConnection()
    {
        var connection = new NpgsqlConnection(_options.ConnectionString);
        connection.Open();

        EnsureInitialized(connection);

        return connection;
    }

    private void EnsureInitialized(NpgsqlConnection connection)
    {
        if (_initialized)
        {
            return;
        }

        lock (_initializationLock)
        {
            if (_initialized)
            {
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE SCHEMA IF NOT EXISTS drawit;

                CREATE SEQUENCE IF NOT EXISTS drawit.users_id_seq AS BIGINT START 1;

                CREATE TABLE IF NOT EXISTS drawit.rooms
                (
                    id TEXT PRIMARY KEY,
                    host_id BIGINT NOT NULL,
                    settings JSONB NOT NULL DEFAULT '{}'::jsonb,
                    status INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS drawit.users
                (
                    id BIGINT PRIMARY KEY DEFAULT nextval('drawit.users_id_seq'),
                    name TEXT NOT NULL,
                    room_id TEXT NULL REFERENCES drawit.rooms(id) ON DELETE SET NULL,
                    is_connected BOOLEAN NOT NULL DEFAULT FALSE,
                    is_ready BOOLEAN NOT NULL DEFAULT FALSE
                );

                CREATE UNIQUE INDEX IF NOT EXISTS users_id_unique ON drawit.users(id);
                CREATE INDEX IF NOT EXISTS users_room_id_index ON drawit.users(room_id);
                ALTER SEQUENCE drawit.users_id_seq OWNED BY drawit.users.id;
            """;

            command.ExecuteNonQuery();

            _initialized = true;
            _logger.LogInformation("PostgreSQL schema ensured at startup");
        }
    }
}
