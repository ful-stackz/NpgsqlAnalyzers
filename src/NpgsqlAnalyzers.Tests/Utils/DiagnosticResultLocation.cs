using System;

namespace NpgsqlAnalyzers.Tests.Utils
{
    internal struct DiagnosticResultLocation
    {
        public DiagnosticResultLocation(string path, int line, int column)
        {
            if (line < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(line), $"Argument {nameof(line)} must be >= -1");
            }

            if (column < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(column), $"Argument {nameof(column)} must be >= -1");
            }

            Path = path;
            Line = line;
            Column = column;
        }

        public string Path { get; }
        public int Line { get; }
        public int Column { get; }
    }
}
