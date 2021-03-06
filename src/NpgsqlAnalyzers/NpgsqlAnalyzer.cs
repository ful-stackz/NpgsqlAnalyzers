using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
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
        private const string ConfigFileName = ".npgsqlanalyzers";

        private Configuration _configuration;

        public NpgsqlAnalyzer()
        {
        }

        public NpgsqlAnalyzer(Configuration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Rules.BadSqlStatement,
            Rules.UndefinedTable,
            Rules.UndefinedColumn,
            Rules.MissingCommand);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction((compilationContext) =>
            {
                if (_configuration is null)
                {
                    var configFile = compilationContext.Options.AdditionalFiles.FirstOrDefault(
                        (file) => Path.GetFileName(file.Path).Equals(ConfigFileName));
                    if (configFile is null)
                    {
                        throw new InvalidOperationException("Missing configuration file.");
                    }

                    _configuration = Configuration.FromFile(
                        configFile.GetText().Lines.Select(line => line.ToString()));
                }

                compilationContext.RegisterSyntaxNodeAction(
                    AnalyzeInvocationExpressionNode,
                    SyntaxKind.ObjectCreationExpression);
            });
        }

        private static void Log(string value)
        {
            var logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                nameof(NpgsqlAnalyzer),
                "log.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(logFile));
            File.AppendAllText(logFile, $"{value}{Environment.NewLine}");
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
        private static string ExtractQuery(string queryLiteral) =>
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

        private static VariableDeclaratorSyntax FindVariableDeclaratorInNodes(
            IEnumerable<SyntaxNode> nodes,
            string variableName)
        {
            return nodes
                .SelectMany(child => child.ChildNodes())
                .OfType<LocalDeclarationStatementSyntax>()
                .Select(localDeclaration => localDeclaration.Declaration)
                .SelectMany(variableDeclaration => variableDeclaration.Variables)
                .Where(variableDeclarator => variableDeclarator.Identifier.Text.Equals(variableName))
                .FirstOrDefault();
        }

        private static IEnumerable<AssignmentExpressionSyntax> FindVariableAssignmentsInNodes(
            IEnumerable<SyntaxNode> nodes,
            Func<string, bool> matchVariableName)
        {
            return nodes
                .SelectMany(node => node.ChildNodes())
                .OfType<ExpressionStatementSyntax>()
                .Select(expressionStatement => expressionStatement.Expression)
                .Where(expression => expression.IsKind(SyntaxKind.SimpleAssignmentExpression))
                .OfType<AssignmentExpressionSyntax>()
                .Where(expression => matchVariableName(expression.Left.ToString()));
        }

        private static IEnumerable<AssignmentExpressionSyntax> FindVariableAssignmentsInNodes(
            IEnumerable<SyntaxNode> nodes,
            string variableName)
        {
            return nodes
                .SelectMany(node => node.ChildNodes())
                .OfType<ExpressionStatementSyntax>()
                .Select(expressionStatement => expressionStatement.Expression)
                .Where(expression => expression.IsKind(SyntaxKind.SimpleAssignmentExpression))
                .OfType<AssignmentExpressionSyntax>()
                .Where(expression => expression.Left.ToString().Equals(variableName));
        }

        private static SqlStatement ExtractSqlStatement(
            SyntaxNodeAnalysisContext context,
            ObjectCreationExpressionSyntax npgsqlCommandCtor)
        {
            if (!npgsqlCommandCtor.ArgumentList.Arguments.Any())
            {
                /**
                 * Query is not passed through the constructor,
                 * therefore it might be assigned to the CommandText prop
                 */
                var commandTextAssignment = FindVariableAssignmentsInNodes(
                    npgsqlCommandCtor.Ancestors(),
                    name => name.Contains($".{nameof(NpgsqlCommand.CommandText)}"))
                    .FirstOrDefault();
                if (commandTextAssignment is null)
                {
                    // Query is not assigned to the CommandText prop
                    return SqlStatement.StatementNotFound;
                }

                if (commandTextAssignment.Right.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    return new SqlStatement(
                        statement: ExtractQuery(commandTextAssignment.Right.ToString()),
                        location: commandTextAssignment.Right.GetLocation());
                }
                else if (commandTextAssignment.Right.IsKind(SyntaxKind.IdentifierName))
                {
                    var varDeclarator = FindVariableDeclaratorInNodes(
                        nodes: npgsqlCommandCtor.Ancestors(),
                        variableName: commandTextAssignment.Right.ToString());
                    var varAssignments = FindVariableAssignmentsInNodes(
                        nodes: npgsqlCommandCtor.Ancestors(),
                        variableName: commandTextAssignment.Right.ToString());

                    if (varAssignments.Any())
                    {
                        var declarationSymbol = context.SemanticModel.GetDeclaredSymbol(varDeclarator);
                        int declarationLine = declarationSymbol.Locations.First().GetLineSpan().StartLinePosition.Line;
                        int commandTextLine = commandTextAssignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                        var variableAssignment = varAssignments
                            .OrderBy(assignment =>
                            {
                                int assignmentLine = assignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                                return Math.Abs(commandTextLine - assignmentLine);
                            })
                            .First();

                        int assignmentLine = variableAssignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                        if (Math.Abs(commandTextLine - assignmentLine) < Math.Abs(commandTextLine - declarationLine))
                        {
                            return new SqlStatement(
                                statement: ExtractQuery(variableAssignment.Right.ToString()),
                                location: variableAssignment.Right.GetLocation());
                        }
                    }

                    // The syntax used to assign a value to NpgsqlCommand.CommandText is not supported
                    return new SqlStatement(
                        statement: ExtractQuery(varDeclarator.Initializer.Value.ToString()),
                        location: varDeclarator.Initializer.Value.GetLocation());
                }

                return default;
            }

            var queryArgument = npgsqlCommandCtor.ArgumentList.Arguments.First();
            if (queryArgument.Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                // Query is defined in the constructor
                return new SqlStatement(
                    statement: ExtractQuery(queryArgument.ToString()),
                    location: npgsqlCommandCtor.GetLocation());
            }

            /**
             * The first constructor argument is not a string literal containing the query,
             * therefore we should search for a variable declaring the query
             */
            string queryVariableName = queryArgument.ToString();

            /**
             * Variable declaration => string query = "";
             * Variable assignment  => query = "assignment after being declared";
             */
            var variableDeclarator = FindVariableDeclaratorInNodes(context.Node.Ancestors(), queryVariableName);
            var variableAssignments = FindVariableAssignmentsInNodes(context.Node.Ancestors(), queryVariableName);

            if (variableAssignments.Any())
            {
                /**
                 * If there are any assignments that means the variable is reused, we should analyze the one that
                 * is closest to the NpgsqlCommand constructor
                 */
                var declarationSymbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator);
                int declarationLine = declarationSymbol.Locations.First().GetLineSpan().StartLinePosition.Line;
                int npgsqlCommandLine = npgsqlCommandCtor.GetLocation().GetLineSpan().StartLinePosition.Line;
                var variableAssignment = variableAssignments
                    .OrderBy(assignment =>
                    {
                        int assignmentLine = assignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                        return Math.Abs(npgsqlCommandLine - assignmentLine);
                    })
                    .First();

                int assignmentLine = variableAssignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                if (Math.Abs(npgsqlCommandLine - assignmentLine) < Math.Abs(npgsqlCommandLine - declarationLine))
                {
                    return new SqlStatement(
                        statement: ExtractQuery(variableAssignment.Right.ToString()),
                        location: variableAssignment.Right.GetLocation());
                }
            }

            return new SqlStatement(
                statement: ExtractQuery(variableDeclarator.Initializer.Value.ToString()),
                location: variableDeclarator.Initializer.Value.GetLocation());
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
            else if (statement.NotFound)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor: Rules.MissingCommand,
                    location: npgsqlCommandExpression.GetLocation()));
            }
        }

        private void ExecuteAndValidateQuery(
            string query,
            SyntaxNodeAnalysisContext context,
            Location sourceLocation)
        {
            try
            {
                using var connection = new NpgsqlConnection(_configuration.ConnectionString);
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
        }
    }
}
