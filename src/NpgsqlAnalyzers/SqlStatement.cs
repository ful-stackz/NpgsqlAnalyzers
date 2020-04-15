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
        }

        public bool IsValid { get; }
        public string Statement { get; }
        public Location Location { get; }
    }
}
