using Microsoft.CodeAnalysis;
using NpgsqlAnalyzers.Tests.Utils;
using NUnit.Framework;
using System;
using ThrowawayDb.Postgres;

namespace NpgsqlAnalyzers.Tests
{
    [TestFixture]
    public class SqlSyntaxValidationTests : IDisposable
    {
        private readonly ThrowawayDatabase _database = Database.CreateDatabase();
        private bool _isDisposed;

        [Test]
        public void AssertNonExistentQueryDetectedInConstructor()
        {
            string source = @"
using Npgsql;

namespace Testing
{
    public class TestClass
    {
        public void TestMethod()
        {
            using var command = new NpgsqlCommand(""SELECT * FROM non_existent_table"");
        }
    }
}
";

            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzers(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1000",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 33),
                    },
                    Message = "The relation referenced at position '15' does not exist.",
                });
        }

        [Test]
        public void AssertNonExistentTableDetectedInDeclaration()
        {
            string source = @"
using Npgsql;

namespace Testing
{
    public class TestClass
    {
        public void TestMethod()
        {
            string query = ""UPDATE bad_table SET id = 1 WHERE name = 'test';"";
            using var command = new NpgsqlCommand(query);
        }
    }
}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzers(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1000",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 28),
                    },
                    Message = "The relation referenced at position '8' does not exist.",
                });
        }

        [Test]
        public void AssertNonExistentTableDetectedInReDeclaration()
        {
            string source = @"
using Npgsql;

namespace Testing
{
    public class TestClass
    {
        public void TestMethod()
        {
            string query = ""SELECT * FROM users;"";
            using var command = new NpgsqlCommand(query);

            query = ""UPDATE bad_table SET id = 1 WHERE name = 'test';"";
            using var command = new NpgsqlCommand(query);
        }
    }
}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzers(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1000",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 13, 21),
                    },
                    Message = "The relation referenced at position '8' does not exist.",
                });
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