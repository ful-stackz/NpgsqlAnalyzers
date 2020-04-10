using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Npgsql;
using System;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace NpgsqlAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NpgsqlAnalyzers : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            Rules.BadSqlStatement);

        public override void Initialize(AnalysisContext context)
        {
            if (string.IsNullOrWhiteSpace(Configuration.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Could not extract database connection string from " +
                    $"environment variable '{Configuration.ConnectionStringEnvVar}'.");
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(
                AnalyzeInvocationExpressionNode,
                SyntaxKind.ObjectCreationExpression);
        }

        private static void AnalyzeInvocationExpressionNode(SyntaxNodeAnalysisContext context)
        {
            var semanticModel = context.SemanticModel;
            var npgsqlCommandExpression = (ObjectCreationExpressionSyntax)context.Node;

            // Check if object creation is NpgsqlCommand
            if (!(semanticModel.GetSymbolInfo(npgsqlCommandExpression).Symbol is IMethodSymbol methodSymbol) ||
                methodSymbol.MethodKind != MethodKind.Constructor ||
                !methodSymbol.ReceiverType.Name.Equals("NpgsqlCommand", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Get first arg for constructor, which is the query
            var queryArgument = npgsqlCommandExpression.ArgumentList.Arguments.First();
            if (queryArgument.Expression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                AnalyzeInConstructorQueryDeclaration(context, queryArgument, npgsqlCommandExpression);
            }
            else
            {
                AnalyzeVarQueryDeclaration(context, semanticModel, queryArgument.ToString(), npgsqlCommandExpression);
            }
        }

        private static void AnalyzeInConstructorQueryDeclaration(
            SyntaxNodeAnalysisContext context,
            ArgumentSyntax queryArgumentSyntax,
            ObjectCreationExpressionSyntax npgsqlCommandExpression)
        {
            try
            {
                ExecuteQuery(ExtractQuery(queryArgumentSyntax.ToString()));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.BadSqlStatement,
                    npgsqlCommandExpression.GetLocation(),
                    ex.Message));
            }
        }

        private static void AnalyzeVarQueryDeclaration(
            SyntaxNodeAnalysisContext context,
            SemanticModel semanticModel,
            string variableName,
            ObjectCreationExpressionSyntax npgsqlCommandExpression)
        {
            var variableDeclarator = context.Node
                .Ancestors()
                .SelectMany(a => a.ChildNodes())
                .OfType<LocalDeclarationStatementSyntax>()
                .Select(localDeclaration => localDeclaration.Declaration)
                .Select(declaration => declaration.Variables.FirstOrDefault())
                .OfType<VariableDeclaratorSyntax>()
                .Where(declarator => declarator.Identifier.Text.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            var declarationSymbol = semanticModel.GetDeclaredSymbol(variableDeclarator);

            var variableAssignment = context.Node
                .Ancestors()
                .SelectMany(a => a.ChildNodes())
                .OfType<ExpressionStatementSyntax>()
                .Where(e => e.Expression.Kind() == SyntaxKind.SimpleAssignmentExpression)
                .Select(e => e.Expression)
                .OfType<AssignmentExpressionSyntax>()
                .Where(e => e.Left.ToString().Equals(variableName, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (variableAssignment is { })
            {
                int declarationLine = declarationSymbol.Locations.First().GetLineSpan().StartLinePosition.Line;
                int assignmentLine = variableAssignment.GetLocation().GetLineSpan().StartLinePosition.Line;
                int npgsqlCommandLine = npgsqlCommandExpression.GetLocation().GetLineSpan().StartLinePosition.Line;

                if (Math.Abs(npgsqlCommandLine - assignmentLine) < Math.Abs(npgsqlCommandLine - declarationLine))
                {
                    // If re-assignment is closer to where the variable is used, analyze the re-assignment
                    try
                    {
                        ExecuteQuery(ExtractQuery(variableAssignment.Right.ToString()));
                    }
                    catch (Exception ex)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Rules.BadSqlStatement,
                            variableAssignment.Right.GetLocation(),
                            ex.Message));
                    }
                    return;
                }
            }

            try
            {
                ExecuteQuery(ExtractQuery(variableDeclarator.Initializer.Value.ToString()));
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.BadSqlStatement,
                    variableDeclarator.Initializer.GetLocation(),
                    ex.Message));
            }
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
        /// A query containing named parameters. For example, <c>SELECT * FROM TABLE WHERE Id = @id;</c>
        /// </param>
        /// <returns>
        /// The same query with the named parameters replaced by <c>NULL</c>.
        /// </returns>
        private static string ReplaceNamedParameters(string query) =>
            Regex.Replace(query, @"@\w+", "NULL");

        private static string ExtractQuery(string literal) =>
            ReplaceNamedParameters(SanitizeQuery(literal));

        private static void ExecuteQuery(string query)
        {
            using var connection = new NpgsqlConnection(Configuration.ConnectionString);
            connection.Open();
            using var command = new NpgsqlCommand(query, connection);
            command.ExecuteReader(CommandBehavior.SchemaOnly);
        }
    }
}
