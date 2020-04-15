using System;
using Microsoft.CodeAnalysis;
using NpgsqlAnalyzers.Tests.Utils;
using NUnit.Framework;
using ThrowawayDb.Postgres;

namespace NpgsqlAnalyzers.Tests
{
    [TestFixture]
    public class SqlSyntaxValidationTests : IDisposable
    {
        private readonly ThrowawayDatabase _database = Database.CreateDatabase();
        private bool _isDisposed;

        [Test]
        public void PSCA1001_DetectedInConstructor()
        {
            const string TableName = "non_existent_table";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            using var command = new NpgsqlCommand(""SELECT * FROM {TableName}"");
        }}
    }}
}}
";

            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1001",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 33),
                    },
                    Message = $"Table '{TableName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1001_DetectedInVariableDeclaration()
        {
            const string TableName = "bad_table";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            string query = ""UPDATE {TableName} SET id = 1 WHERE name = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1001",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 28),
                    },
                    Message = $"Table '{TableName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1001_DetectedInVariableReDeclaration()
        {
            const string TableName = "bad_table";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            string query = ""SELECT * FROM users;"";
            using var command = new NpgsqlCommand(query);

            query = ""UPDATE {TableName} SET id = 1 WHERE name = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1001",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 13, 21),
                    },
                    Message = $"Table '{TableName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1002_DetectedInConstructor()
        {
            const string ColumnName = "non_existent_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            using var command = new NpgsqlCommand(""SELECT {ColumnName} FROM users"");
        }}
    }}
}}
";

            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 33),
                    },
                    Message = $"Column '{ColumnName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1002_DetectedInVariableDeclaration()
        {
            const string ColumnName = "bad_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            string query = ""UPDATE users SET is_admin = 1 WHERE {ColumnName} = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}

        public void TestMethod2()
        {{
            string query = ""UPDATE users SET {ColumnName} = 1 WHERE username = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 28),
                    },
                    Message = $"Column '{ColumnName}' does not exist.",
                },
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 16, 28),
                    },
                    Message = $"Column '{ColumnName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1002_DetectedInVariableReDeclaration()
        {
            const string ColumnName = "bad_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            string query = ""SELECT * FROM users;"";
            using var command = new NpgsqlCommand(query);

            query = ""UPDATE users SET {ColumnName} = 1 WHERE username = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}

        public void TestMethod2()
        {{
            string query = ""SELECT * FROM users;"";
            using var command = new NpgsqlCommand(query);

            query = ""UPDATE posts SET is_admin = 1 WHERE {ColumnName} = 'test';"";
            using var command = new NpgsqlCommand(query);
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 13, 21),
                    },
                    Message = $"Column '{ColumnName}' does not exist.",
                },
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 22, 21),
                    },
                    Message = $"Column '{ColumnName}' does not exist.",
                });
        }

        [Test]
        public void PSCA1002_DetectedInCommandTextProp()
        {
            const string ColumnName = "bad_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void CommandTextLiteral()
        {{
            using var command = new NpgsqlCommand();
            command.CommandText = ""SELECT {ColumnName} FROM users"";
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Message = $"Column '{ColumnName}' does not exist.",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 11, 35),
                    },
                });
        }

        [Test]
        public void PSCA1002_DetectedInCommandTextPropAsVariable()
        {
            const string ColumnName = "bad_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void CommandTextLiteral()
        {{
            string query = ""SELECT {ColumnName} FROM users"";
            using var command = new NpgsqlCommand();
            command.CommandText = query;
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Message = $"Column '{ColumnName}' does not exist.",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 28),
                    },
                });
        }

        [Test]
        public void PSCA1002_DetectedInCommandTextPropAsVariableRedeclaration()
        {
            const string ColumnName = "bad_column";
            string source = @$"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void CommandTextLiteral()
        {{
            string query = ""SELECT id FROM users"";
            using var command = new NpgsqlCommand();
            query = ""SELECT {ColumnName} FROM users"";
            command.CommandText = query;
        }}
    }}
}}
";
            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1002",
                    Message = $"Column '{ColumnName}' does not exist.",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 12, 21),
                    },
                });
        }

        [Test]
        public void PSCA1100_DetectedMissingCommand()
        {
            string source = @"
using Npgsql;

namespace Testing
{{
    public class TestClass
    {{
        public void TestMethod()
        {{
            using var command = new NpgsqlCommand();
        }}
    }}
}}
";

            Diagnostics.AnalyzeSourceCode(
                source,
                new NpgsqlAnalyzer(_database.ConnectionString),
                new DiagnosticResult
                {
                    Id = "PSCA1100",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new DiagnosticResultLocation[]
                    {
                        new DiagnosticResultLocation("Test0.cs", 10, 33),
                    },
                    Message = "Provide a SQL statement via the constructor or the CommandText property.",
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
