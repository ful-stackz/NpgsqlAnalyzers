using NpgsqlAnalyzers.Tests.Utils;
using NUnit.Framework;

namespace NpgsqlAnalyzers.Tests
{
    [TestFixture]
    public class RegressionTests
    {
        [Test]
        public void AssertGoodFullDatabaseQueryDoesntFail()
        {
            string source = @"
using Npgsql;

namespace Testing
{
    public class TestClass
    {
        public void QueryInConstructor()
        {
            using var command = new NpgsqlCommand(
                @""
                    SELECT users.id, users.username, users.is_admin, users.created_at, posts.id, posts.title, posts.body, posts.created_at, user_posts.user_id, user_posts.post_id
                    FROM users
                    JOIN user_posts ON user_posts.user_id = users.id
                    JOIN posts ON posts.id = user_posts.post_id;
                "");
        }

        public void QueryAsVariable()
        {
            string query = @""
                SELECT users.id, users.username, users.is_admin, users.created_at, posts.id, posts.title, posts.body, posts.created_at, user_posts.user_id, user_posts.post_id
                FROM users
                JOIN user_posts ON user_posts.user_id = users.id
                JOIN posts ON posts.id = user_posts.post_id;
            "";
            using var command = new NpgsqlCommand(query);
        }

        public void QueryAsReDeclaredVariable()
        {
            string query = ""invalid query syntax"";

            query = @""
                SELECT users.id, users.username, users.is_admin, users.created_at, posts.id, posts.title, posts.body, posts.created_at, user_posts.user_id, user_posts.post_id
                FROM users
                JOIN user_posts ON user_posts.user_id = users.id
                JOIN posts ON posts.id = user_posts.post_id;
            "";
            using var command = new NpgsqlCommand(query);
        }
    }
}
            ";

            using var database = Database.CreateDatabase();
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(database.ConnectionString));
        }
    }
}
