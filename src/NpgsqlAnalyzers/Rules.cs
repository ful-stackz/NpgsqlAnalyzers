using Microsoft.CodeAnalysis;

namespace NpgsqlAnalyzers
{
    internal static class Rules
    {
        public static DiagnosticDescriptor BadSqlStatement { get; } = new DiagnosticDescriptor(
            id: "PSCA1000",
            title: "Bad SQL statement.",
            messageFormat: "{0}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static DiagnosticDescriptor UndefinedTable { get; } = new DiagnosticDescriptor(
            id: "PSCA1001",
            title: "Undefined table.",
            messageFormat: "Table '{0}' does not exist.",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
