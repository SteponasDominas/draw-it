using System.Data;
using System.Text.Json;
using Draw.it.Server.Enums;
using Draw.it.Server.Models.Room;
using Draw.it.Server.Repositories.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace Draw.it.Server.Repositories.Room;

public class PostgresRoomRepository : IRoomRepository
{
    private readonly IPostgresConnectionFactory _connectionFactory;

    public PostgresRoomRepository(IPostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Save(RoomModel room)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO drawit.rooms (id, host_id, settings, status)
            VALUES (@id, @host_id, @settings, @status)
            ON CONFLICT (id) DO UPDATE SET
                host_id = EXCLUDED.host_id,
                settings = EXCLUDED.settings,
                status = EXCLUDED.status;
        """;

        AddParameter(command, "@id", NpgsqlDbType.Text, room.Id);
        AddParameter(command, "@host_id", NpgsqlDbType.Bigint, room.HostId);
        AddParameter(command, "@settings", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(room.Settings ?? new RoomSettingsModel()));
        AddParameter(command, "@status", NpgsqlDbType.Integer, (int)room.Status);

        command.ExecuteNonQuery();
    }

    public bool DeleteById(string id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM drawit.rooms WHERE id = @id";
        AddParameter(command, "@id", NpgsqlDbType.Text, id);

        var affected = command.ExecuteNonQuery();
        return affected > 0;
    }

    public RoomModel? FindById(string id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, host_id, settings, status
            FROM drawit.rooms
            WHERE id = @id;
        """;
        AddParameter(command, "@id", NpgsqlDbType.Text, id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapRoom(reader);
    }

    public IEnumerable<RoomModel> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, host_id, settings, status FROM drawit.rooms";

        using var reader = command.ExecuteReader();
        var results = new List<RoomModel>();
        while (reader.Read())
        {
            results.Add(MapRoom(reader));
        }

        return results;
    }

    public bool ExistsById(string id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM drawit.rooms WHERE id = @id";
        AddParameter(command, "@id", NpgsqlDbType.Text, id);

        using var reader = command.ExecuteReader();
        return reader.Read();
    }

    private static void AddParameter(IDbCommand command, string name, NpgsqlDbType dbType, object value)
    {
        if (command is not NpgsqlCommand npgsqlCommand)
        {
            throw new InvalidOperationException("Expected NpgsqlCommand when interacting with PostgreSQL.");
        }

        npgsqlCommand.Parameters.Add(new NpgsqlParameter
        {
            ParameterName = name,
            NpgsqlDbType = dbType,
            Value = value
        });
    }

    private static RoomModel MapRoom(IDataRecord record)
    {
        var settingsJson = record.IsDBNull(2) ? null : record.GetString(2);
        var settings = settingsJson is null
            ? new RoomSettingsModel()
            : JsonSerializer.Deserialize<RoomSettingsModel>(settingsJson) ?? new RoomSettingsModel();

        return new RoomModel
        {
            Id = record.GetString(0),
            HostId = record.GetInt64(1),
            Settings = settings,
            Status = (RoomStatus)record.GetInt32(3)
        };
    }
}
