using Npgsql;
using ThrowawayDb.Postgres;

namespace NpgsqlAnalyzers.Tests.Utils
{
    internal class Database
    {
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=postgres;";

        public static ThrowawayDatabase CreateDatabase()
        {
            var database = ThrowawayDatabase.Create(ConnectionString);
            using var connection = new NpgsqlConnection(database.ConnectionString);
            connection.Open();
            InitializeDatabase(connection);
            return database;
        }

        private static void InitializeDatabase(NpgsqlConnection connection)
        {
            CreateUsersTable(connection);
            CreatePostsTable(connection);
            CreateUserPostsTable(connection);
        }

        private static void CreateUsersTable(NpgsqlConnection connection)
        {
            string query = @"
                CREATE TABLE users (
                    id serial,
                    username text not null,
                    is_admin boolean not null,
                    created_at timestamp not null,
                    primary key (id),
                    unique (username));";

            using var command = new NpgsqlCommand(query, connection);
            _ = command.ExecuteNonQuery();
        }

        private static void CreatePostsTable(NpgsqlConnection connection)
        {
            string query = @"
                CREATE TABLE posts (
                    id serial,
                    title text not null,
                    body text not null,
                    created_at timestamp not null,
                    primary key (id));";

            using var command = new NpgsqlCommand(query, connection);
            _ = command.ExecuteNonQuery();
        }

        private static void CreateUserPostsTable(NpgsqlConnection connection)
        {
            string query = @"
                CREATE TABLE user_posts (
                    user_id int not null,
                    post_id int not null,
                    primary key (user_id, post_id),
                    foreign key (user_id) references users(id) on delete cascade,
                    foreign key (post_id) references posts(id) on delete cascade);";

            using var command = new NpgsqlCommand(query, connection);
            _ = command.ExecuteNonQuery();
        }
    }
}
