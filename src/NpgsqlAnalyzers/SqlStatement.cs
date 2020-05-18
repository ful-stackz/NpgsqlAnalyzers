using System;
using Microsoft.CodeAnalysis;

namespace NpgsqlAnalyzers
{
    internal struct SqlStatement
    {
        public SqlStatement(string statement, Location location)
        {
            Statement = statement;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            IsValid = true;
            NotFound = false;
        }

        private SqlStatement(string statement, Location location, bool isValid, bool notFound)
        {
            Statement = statement;
            Location = location;
            IsValid = isValid;
            NotFound = notFound;
        }

        public static SqlStatement StatementNotFound { get; } = new SqlStatement(
            statement: string.Empty,
            location: null,
            isValid: false,
            notFound: true);

        public bool IsValid { get; }
        public bool NotFound { get; }
        public string Statement { get; }
        public Location Location { get; }
    }
}
