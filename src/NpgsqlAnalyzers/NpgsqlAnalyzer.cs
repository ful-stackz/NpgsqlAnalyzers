using System;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Npgsql;

namespace NpgsqlAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NpgsqlAnalyzer : DiagnosticAnalyzer
    {
        private readonly string _connectionString;

        public NpgsqlAnalyzer()
            : this(Configuration.ConnectionString)
        {
        }

        public NpgsqlAnalyzer(string connectionString)
        {
            _connectionString = string.IsNullOrWhiteSpace(connectionString)
                ? throw new InvalidOperationException("Invalid connection string.")
                : connectionString;
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Rules.BadSqlStatement,
            Rules.UndefinedTable,
            Rules.UndefinedColumn);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(
                AnalyzeInvocationExpressionNode,
                SyntaxKind.ObjectCreationExpression);
        }

        /// <summary>
        /// Extracts the pure query from a literal.
        /// </summary>
        /// <param name="queryLiteral">
        /// A query literal in the form of <c>"{query}"</c> or <c>@"{query}"</c>.
        /// </param>
        /// <remarks>
        /// A query literal is retrieved from the analysis context in the form of <c>"{query}"</c> or <c>@"{query}"</c>.
        /// </remarks>
        /// <returns>
        /// The pure query, without the enclosing quotes and @.
        /// </returns>
        private static string SanitizeQuery(string queryLiteral) =>
            queryLiteral
                .Trim()
                .Substring(1) // Removes the @ or " at the start of the string definition
                .Replace("\"", string.Empty);

        /// <summary>
        /// Replaces named parameters inside the query with <c>NULL</c>.
        /// </summary>
        /// <param name="query">
        /// A query containing named parameters. For example, <c>SELECT * FROM TABLE WHERE Id = @id;</c>.
        /// </param>
        /// <returns>
        /// The same query with the named parameters replaced by <c>NULL</c>.
        /// </returns>
        private static string ReplaceNamedParameters(string query) =>
            Regex.Replace(query, @"@\w+", "NULL");

        private static SqlStatement ExtractSqlStatement(
            SyntaxNodeAnalysisContext context,
            ObjectCreationExpressionSyntax npgsqlCommandCtor)
        {
            var queryArgument = npgsqlCommandCtor.ArgumentList.Arguments.First();
            if (queryArgument.Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                // Query is defined in the constructor
                return new SqlStatement(
                    statement: SanitizeQuery(queryArgument.ToString()),
                    location: npgsqlCommandCtor.GetLocation());
            }

            // Search for query declaration in ancestor variables
            string queryVariableName = queryArgument.ToString();
            var variableDeclarator = context.Node
                .Ancestors()
                .SelectMany(a => a.ChildNodes())
                .OfType<LocalDeclarationStatementSyntax>()
                .Select(localDeclaration => localDeclaration.Declaration)
                .Select(declaration => declaration.Variables.FirstOrDefault())
                .OfType<VariableDeclaratorSyntax>()
                .Where(declarator => declarator.Identifier.Text.Equals(queryVariableName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            var declarationSymbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator);

            var variableAssignment = context.Node
                .Ancestors()
                .SelectMany(a => a.ChildNodes())
                .OfType<ExpressionStatementSyntax>()
                .Where(e => e.Expression.Kind() == SyntaxKind.SimpleAssignmentExpression)
                .Select(e => e.Expression)
                .OfType<AssignmentExpressionSyntax>()
                .Where(e => e.Left.ToString().Equals(queryVariableName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (variableAssignment is { })
            {
                int declarationLine = declarationSymbol.Locations.First().GetLineSpan().StartLinePosition.Line;
                int assignmentLine = variableAssignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                int npgsqlCommandLine = npgsqlCommandCtor.GetLocation().GetLineSpan().StartLinePosition.Line;

                if (Math.Abs(npgsqlCommandLine - assignmentLine) < Math.Abs(npgsqlCommandLine - declarationLine))
                {
                    return new SqlStatement(
                        statement: SanitizeQuery(variableAssignment.Right.ToString()),
                        location: variableAssignment.Right.GetLocation());
                }
            }

            return new SqlStatement(
                statement: SanitizeQuery(variableDeclarator.Initializer.Value.ToString()),
                location: variableDeclarator.Initializer
                    .ChildNodes()
                    .OfType<LiteralExpressionSyntax>()
                    .First()
                    .GetLocation());
        }

        private void AnalyzeInvocationExpressionNode(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var npgsqlCommandExpression = (ObjectCreationExpressionSyntax)context.Node;

            // Check if object creation is NpgsqlCommand
            if (!(semanticModel.GetSymbolInfo(npgsqlCommandExpression).Symbol is IMethodSymbol methodSymbol) ||
                methodSymbol.MethodKind != MethodKind.Constructor ||
                !methodSymbol.ReceiverType.Name.Equals(nameof(NpgsqlCommand), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var statement = ExtractSqlStatement(context, npgsqlCommandExpression);
            if (statement.IsValid)
            {
                ExecuteAndValidateQuery(
                    query: ReplaceNamedParameters(statement.Statement),
                    context: context,
                    sourceLocation: statement.Location);
            }
        }

        private void ExecuteAndValidateQuery(
            string query,
            SyntaxNodeAnalysisContext context,
            Location sourceLocation)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                using var command = new NpgsqlCommand(query, connection);
                command.ExecuteReader(CommandBehavior.SchemaOnly);
            }
            catch (PostgresException ex)
            {
                switch (ex.SqlState)
                {
                    case PostgresErrorCodes.UndefinedTable:
                        string table = Regex.Match(ex.Statement.SQL.Substring(ex.Position - 1), @"\w+").Value;
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: Rules.UndefinedTable,
                            location: sourceLocation,
                            messageArgs: table));
                        break;

                    case PostgresErrorCodes.UndefinedColumn:
                        string column = Regex.Match(ex.Statement.SQL.Substring(ex.Position - 1), @"\w+").Value;
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: Rules.UndefinedColumn,
                            location: sourceLocation,
                            messageArgs: column));
                        break;

                    default:
                        context.ReportDiagnostic(Diagnostic.Create(
                            descriptor: Rules.BadSqlStatement,
                            location: sourceLocation,
                            ex.Message));
                        break;
                }
            }
            catch (Exception)
            {
                // Ignore
            }
        }
    }
}
