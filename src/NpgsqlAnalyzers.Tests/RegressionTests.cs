using System;
using NpgsqlAnalyzers.Tests.Utils;
using NUnit.Framework;
using ThrowawayDb.Postgres;

namespace NpgsqlAnalyzers.Tests
{
    [TestFixture]
    public class RegressionTests : IDisposable
    {
        private static readonly ThrowawayDatabase _database = Database.CreateDatabase();
        private bool _isDisposed;

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

            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                if (isDisposing)
                {
                    _database.Dispose();
                }

                _isDisposed = true;
            }
        }
    }
}
