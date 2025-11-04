using Draw.it.Server.Enums;
using Draw.it.Server.Repositories.Room;
using Draw.it.Server.Repositories.WordPool;
using Draw.it.Server.Repositories.User;

namespace Draw.it.Server.Repositories;


public static class RepositoryDependencyInjection
{
    public static IServiceCollection AddApplicationRepositories(this IServiceCollection services, IConfiguration config)
    {
        var repoTypeValue = config.GetValue<string>("RepositoryType");
        _ = Enum.TryParse<RepoType>(repoTypeValue, ignoreCase: true, out var repoType);

        switch (repoType)
        {
            case RepoType.Db:
                var connectionString = config.GetConnectionString("Postgres")
                    ?? config.GetValue<string>("Postgres:ConnectionString");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("PostgreSQL connection string was not provided.");
                }

                services.AddSingleton(new Postgres.PostgresOptions
                {
                    ConnectionString = connectionString
                });
                services.AddSingleton<Postgres.IPostgresConnectionFactory, Postgres.PostgresConnectionFactory>();
                services.AddSingleton<IUserRepository, User.PostgresUserRepository>();
                services.AddSingleton<IRoomRepository, Room.PostgresRoomRepository>();
                services.AddSingleton<IWordPoolRepository, FileStreamWordPoolRepository>();
                break;

            case RepoType.InMem:
            default:
                services.AddSingleton<IUserRepository, InMemUserRepository>();
                services.AddSingleton<IRoomRepository, InMemRoomRepository>();
                services.AddSingleton<IWordPoolRepository, FileStreamWordPoolRepository>();
                break;
        }

        return services;
    }
}