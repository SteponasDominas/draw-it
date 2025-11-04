using System.Data;
using Draw.it.Server.Models.User;
using Draw.it.Server.Repositories.Postgres;
using Npgsql;
using NpgsqlTypes;

namespace Draw.it.Server.Repositories.User;

public class PostgresUserRepository : IUserRepository
{
    private readonly IPostgresConnectionFactory _connectionFactory;

    public PostgresUserRepository(IPostgresConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void Save(UserModel user)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO drawit.users (id, name, room_id, is_connected, is_ready)
            VALUES (@id, @name, @room_id, @is_connected, @is_ready)
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                room_id = EXCLUDED.room_id,
                is_connected = EXCLUDED.is_connected,
                is_ready = EXCLUDED.is_ready;
        """;

        AddParameter(command, "@id", NpgsqlDbType.Bigint, user.Id);
        AddParameter(command, "@name", NpgsqlDbType.Text, user.Name);
        AddParameter(command, "@room_id", NpgsqlDbType.Text, (object?)user.RoomId ?? DBNull.Value);
        AddParameter(command, "@is_connected", NpgsqlDbType.Boolean, user.IsConnected);
        AddParameter(command, "@is_ready", NpgsqlDbType.Boolean, user.IsReady);

        command.ExecuteNonQuery();
    }

    public bool DeleteById(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM drawit.users WHERE id = @id";
        AddParameter(command, "@id", NpgsqlDbType.Bigint, id);

        var affected = command.ExecuteNonQuery();
        return affected > 0;
    }

    public UserModel? FindById(long id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, room_id, is_connected, is_ready
            FROM drawit.users
            WHERE id = @id;
        """;
        AddParameter(command, "@id", NpgsqlDbType.Bigint, id);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return MapUser(reader);
    }

    public IEnumerable<UserModel> GetAll()
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, room_id, is_connected, is_ready FROM drawit.users";

        using var reader = command.ExecuteReader();
        var results = new List<UserModel>();
        while (reader.Read())
        {
            results.Add(MapUser(reader));
        }

        return results;
    }

    public long GetNextId()
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nextval('drawit.users_id_seq')";

        var result = command.ExecuteScalar();
        if (result is long longResult)
        {
            return longResult;
        }

        if (result is decimal decimalResult)
        {
            return (long)decimalResult;
        }

        throw new InvalidOperationException("Unable to retrieve next user id from sequence.");
    }

    public IEnumerable<UserModel> FindByRoomId(string roomId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, room_id, is_connected, is_ready
            FROM drawit.users
            WHERE room_id = @room_id;
        """;
        AddParameter(command, "@room_id", NpgsqlDbType.Text, roomId);

        using var reader = command.ExecuteReader();
        var results = new List<UserModel>();
        while (reader.Read())
        {
            results.Add(MapUser(reader));
        }

        return results;
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

    private static UserModel MapUser(IDataRecord record)
    {
        return new UserModel
        {
            Id = record.GetInt64(0),
            Name = record.GetString(1),
            RoomId = record.IsDBNull(2) ? null : record.GetString(2),
            IsConnected = record.GetBoolean(3),
            IsReady = record.GetBoolean(4)
        };
    }
}
