using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Npgsql;
using NUnit.Framework;

namespace NpgsqlAnalyzers.Tests.Utils
{
    internal class Diagnostics
    {
        private const string DefaultFileExt = "cs";
        private const string TestFilePrefix = "Test";
        private const string TestProjectName = "TestProject";

        private static readonly MetadataReference CorlibReference = MetadataReference
            .CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference SystemCoreReference = MetadataReference
            .CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference CSharpSymbolsReference = MetadataReference
            .CreateFromFile(typeof(CSharpCompilation).Assembly.Location);
        private static readonly MetadataReference CodeAnalysisReference = MetadataReference
            .CreateFromFile(typeof(Compilation).Assembly.Location);
        private static readonly MetadataReference NpgsqlReference = MetadataReference
            .CreateFromFile(typeof(NpgsqlCommand).Assembly.Location);

        /// <summary>
        /// Analyzes the specified <paramref name="source"/> code with the specified <paramref name="analyzer"/>.
        /// The produced diagnostics, if any, are then verified against the specified <paramref name="expected"/>
        /// <see cref="DiagnosticResult"/>s.
        /// </summary>
        /// <param name="source">
        /// Represents the content of a source code file that will be analyzed.
        /// </param>
        /// <param name="analyzer">
        /// The analyzer to be run on the source code.
        /// </param>
        /// <param name="expected">
        /// A collection of <see cref="DiagnosticResult"/>s which were expected to appear after the analysis.
        /// </param>
        /// <exception cref="AssertionException">
        /// When the diagnostics found during the analysis do not match the <paramref name="expected"/> diagnostics.
        /// </exception>
        public static void AnalyzeSourceCode(
            string source,
            DiagnosticAnalyzer analyzer,
            params DiagnosticResult[] expected)
        {
            var diagnostics = GetDiagnostics(new string[] { source }, analyzer);
            VerifyDiagnosticResults(diagnostics, analyzer, expected);
        }

        /// <summary>
        /// Analyzes each of the specified source codes from <paramref name="sources"/> with the specified
        /// <paramref name="analyzer"/>. The produced diagnostics, if any, are then verified against the specified
        /// <paramref name="expected"/> <see cref="DiagnosticResult"/>s.
        /// </summary>
        /// <param name="sources">
        /// Each <see cref="string"/> represents the content of a source code file that will be analyzed.
        /// </param>
        /// <param name="analyzer">
        /// The analyzer to be run on the source code.
        /// </param>
        /// <param name="expected">
        /// A collection of <see cref="DiagnosticResult"/>s which were expected to appear after the analysis.
        /// </param>
        /// <exception cref="AssertionException">
        /// When the diagnostics found during the analysis do not match the <paramref name="expected"/> diagnostics.
        /// </exception>
        public static void AnalyzeSourceCode(
            IEnumerable<string> sources,
            DiagnosticAnalyzer analyzer,
            params DiagnosticResult[] expected)
        {
            var diagnostics = GetDiagnostics(sources.ToArray(), analyzer);
            VerifyDiagnosticResults(diagnostics, analyzer, expected);
        }

        /// <summary>
        /// Verifies each of the <paramref name="foundDiagnostics"/> and compares them with the corresponding
        /// <paramref name="expectedDiagnostics"/>. Diagnostics are considered equal only if the
        /// <see cref="DiagnosticResultLocation"/>, <see cref="DiagnosticResult.Id"/>,
        /// <see cref="DiagnosticResult.Severity"/> and <see cref="DiagnosticResult.Message"/> of the expected
        /// diagnostics match the corresponding found diagnostics.
        /// </summary>
        /// <param name="foundDiagnostics">
        /// The <see cref="Diagnostic"/>s found by the specified <paramref name="analyzer"/>.
        /// </param>
        /// <param name="analyzer">
        /// The analyzer used to produce the specified <paramref name="foundDiagnostics"/>.
        /// </param>
        /// <param name="expectedDiagnostics">
        /// A collection of <see cref="DiagnosticResult"/>s which were expected to appear after the analysis.
        /// </param>
        private static void VerifyDiagnosticResults(
            IEnumerable<Diagnostic> foundDiagnostics,
            DiagnosticAnalyzer analyzer,
            params DiagnosticResult[] expectedDiagnostics)
        {
            int expectedCount = expectedDiagnostics.Count();
            int actualCount = foundDiagnostics.Count();

            if (expectedCount != actualCount)
            {
                string diagnosticsOutput = foundDiagnostics.Any()
                    ? FormatDiagnostics(analyzer, foundDiagnostics.ToArray())
                    : "NONE";

                Assert.Fail(
                    CreateErrorMessage(
                        header: "Mismatch between number of diagnostics returned.",
                        expected: $"Expected to find '{expectedCount}' diagnostics.",
                        actual: $"Found '{actualCount}' diagnostics.",
                        diagnostic: diagnosticsOutput));
            }

            for (int i = 0; i < expectedDiagnostics.Length; i++)
            {
                var actual = foundDiagnostics.ElementAt(i);
                var expected = expectedDiagnostics[i];

                if (expected.Line == -1 && expected.Column == -1)
                {
                    if (actual.Location != Location.None)
                    {
                        Assert.Fail(
                            CreateErrorMessage(
                                expected: "A project diagnostic with no location.",
                                actual: FormatDiagnostics(analyzer, actual)));
                    }
                }
                else
                {
                    VerifyDiagnosticLocation(analyzer, actual, actual.Location, expected.Locations.First());
                    var additionalLocations = actual.AdditionalLocations.ToArray();

                    if (additionalLocations.Length != expected.Locations.Length - 1)
                    {
                        Assert.Fail(
                            CreateErrorMessage(
                                expected: $"Expected '{expected.Locations.Length - 1}' additional locations.",
                                actual: $"Got '{additionalLocations.Length}' additional locations.",
                                diagnostic: FormatDiagnostics(analyzer, actual)));
                    }

                    for (int j = 0; j < additionalLocations.Length; ++j)
                    {
                        VerifyDiagnosticLocation(analyzer, actual, additionalLocations[j], expected.Locations[j + 1]);
                    }
                }

                if (actual.Id != expected.Id)
                {
                    Assert.Fail(
                        CreateErrorMessage(
                            expected: $"Expected diagnostic id to be '{expected.Id}'.",
                            actual: $"Diagnostic id was '{actual.Id}'.",
                            diagnostic: FormatDiagnostics(analyzer, actual)));
                }

                if (actual.Severity != expected.Severity)
                {
                    Assert.Fail(
                        CreateErrorMessage(
                            expected: $"Expected diagnostic severity to be '{expected.Severity}'.",
                            actual: $"Diagnostic severity was '{actual.Severity}'.",
                            diagnostic: FormatDiagnostics(analyzer, actual)));
                }

                if (actual.GetMessage() != expected.Message)
                {
                    Assert.Fail(
                        CreateErrorMessage(
                            expected: $"Expected diagnostic message to be '{expected.Message}'.",
                            actual: $"Diagnostic message was '{actual.GetMessage()}'.",
                            diagnostic: FormatDiagnostics(analyzer, actual)));
                }
            }
        }

        /// <summary>
        /// Verifies that the specified <paramref name="diagnostic"/> location matches the
        /// <paramref name="expected"/> location.
        /// </summary>
        /// <param name="analyzer">
        /// The analyzer used to produce the specified <paramref name="diagnostic"/>.
        /// </param>
        /// <param name="diagnostic">
        /// The detected <see cref="Diagnostic"/>.
        /// </param>
        /// <param name="actual">
        /// The actual <see cref="Location"/> of the <paramref name="diagnostic"/> in the source code.
        /// </param>
        /// <param name="expected">
        /// The expected location where the <paramref name="diagnostic"/> should have been found.
        /// </param>
        private static void VerifyDiagnosticLocation(
            DiagnosticAnalyzer analyzer,
            Diagnostic diagnostic,
            Location actual,
            DiagnosticResultLocation expected)
        {
            var actualSpan = actual.GetLineSpan();

            Assert.IsTrue(
                condition:
                    actualSpan.Path == expected.Path ||
                    (actualSpan.Path is { } && actualSpan.Path.Contains("Test0.") && expected.Path.Contains("Test.")),
                message: CreateErrorMessage(
                    expected: $"Diagnostic to be in file '{expected.Path}'.",
                    actual: $"Diagnostic was in file '{actualSpan.Path}'.",
                    diagnostic: FormatDiagnostics(analyzer, diagnostic)));

            var actualLinePosition = actualSpan.StartLinePosition;

            // Only check line position if there is an actual line in the real diagnostic
            if (actualLinePosition.Line > 0)
            {
                if (actualLinePosition.Line + 1 != expected.Line)
                {
                    Assert.Fail(
                        CreateErrorMessage(
                            expected: $"Diagnostic to be on line '{expected.Line}'.",
                            actual: $"Diagnostic was on line '{actualLinePosition.Line + 1}'.",
                            diagnostic: FormatDiagnostics(analyzer, diagnostic)));
                }
            }

            // Only check column position if there is an actual column position in the real diagnostic
            if (actualLinePosition.Character > 0)
            {
                if (actualLinePosition.Character + 1 != expected.Column)
                {
                    Assert.Fail(
                        CreateErrorMessage(
                            expected: $"Diagnostic to start at column '{expected.Column}'.",
                            actual: $"Diagnostic started at column '{actualLinePosition.Character + 1}'.",
                            diagnostic: FormatDiagnostics(analyzer, diagnostic)));
                }
            }
        }

        private static string CreateErrorMessage(
            string expected,
            string actual,
            string header = "",
            string diagnostic = "")
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(header))
            {
                builder.Append(header);
                builder.Append(Environment.NewLine);
                builder.Append(Environment.NewLine);
            }

            builder.Append("Expected:");
            builder.Append(Environment.NewLine);
            builder.Append(expected);
            builder.Append(Environment.NewLine);
            builder.Append("Actual:");
            builder.Append(Environment.NewLine);
            builder.Append(actual);

            if (!string.IsNullOrEmpty(diagnostic))
            {
                builder.Append(Environment.NewLine);
                builder.Append(Environment.NewLine);
                builder.Append("Diagnostic:");
                builder.Append(Environment.NewLine);
                builder.Append(diagnostic);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats the specified <paramref name="diagnostics"/> into a readable <see cref="string"/>.
        /// </summary>
        /// <param name="analyzer">
        /// The analyzer used to produce the specified <paramref name="diagnostics"/>.
        /// </param>
        /// <param name="diagnostics">
        /// The <see cref="Diagnostic"/>s to be formatted.
        /// </param>
        /// <returns>
        /// The specified <paramref name="diagnostics"/> as a readable <see cref="string"/>.
        /// </returns>
        private static string FormatDiagnostics(DiagnosticAnalyzer analyzer, params Diagnostic[] diagnostics)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < diagnostics.Length; ++i)
            {
                builder.AppendLine("// " + diagnostics[i].ToString());

                var analyzerType = analyzer.GetType();
                var rules = analyzer.SupportedDiagnostics;

                foreach (var rule in rules)
                {
                    if (rule is null || rule.Id != diagnostics[i].Id)
                    {
                        continue;
                    }

                    var location = diagnostics[i].Location;
                    if (location == Location.None)
                    {
                        builder.AppendFormat($"GetGlobalResult({analyzerType.Name}.{rule.Id})");
                    }
                    else
                    {
                        Assert.IsTrue(
                            condition: location.IsInSource,
                            message: "Test base does not currently handle diagnostics in metadata locations. " +
                            $"Diagnostic in metadata: {diagnostics[i]}\r\n");

                        string resultMethodName = "GetCSharpResultAt";
                        var linePosition = diagnostics[i].Location.GetLineSpan().StartLinePosition;

                        builder.AppendFormat(
                            "{0}({1}, {2}, {3}.{4})",
                            resultMethodName,
                            linePosition.Line + 1,
                            linePosition.Character + 1,
                            analyzerType.Name,
                            rule.Id);
                    }

                    if (i != diagnostics.Length - 1)
                    {
                        builder.Append(',');
                    }

                    builder.AppendLine();
                    break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Applies the specified <paramref name="analyzer"/> to the specified <paramref name="sources"/> and
        /// returns the diagnostics found during the analysis.
        /// </summary>
        /// <param name="sources">
        /// Each <see cref="string"/> represents the content of a source code file.
        /// </param>
        /// <param name="analyzer">
        /// The analyzer to be run on the <paramref name="sources"/>.
        /// </param>
        /// <returns>
        /// A collection of <see cref="Diagnostic"/>s found by the specified <paramref name="analyzer"/> during the
        /// analysis of the source code contained in the specified <paramref name="sources"/>.
        /// </returns>
        private static Diagnostic[] GetDiagnostics(string[] sources, DiagnosticAnalyzer analyzer) =>
            GetDiagnosticsFromDocuments(analyzer, GetDocuments(sources));

        /// <summary>
        /// Applies the specified <paramref name="analyzer"/> to the specified <paramref name="documents"/> and
        /// returns the diagnostics found during the analysis, ordered by their position.
        /// </summary>
        /// <param name="analyzer">
        /// The analyzer to run on the documents.
        /// </param>
        /// <param name="documents">
        /// The documents to be analyzed.
        /// </param>
        /// <returns>
        /// A collection of <see cref="Diagnostic"/>s found by the specified <paramref name="analyzer"/> during the
        /// analysis of the source code contained in the specified <paramref name="documents"/>,
        /// sorted by their <see cref="Location"/>.
        /// </returns>
        private static Diagnostic[] GetDiagnosticsFromDocuments(
            DiagnosticAnalyzer analyzer,
            Document[] documents)
        {
            var projects = new HashSet<Project>(documents.Select(doc => doc.Project));
            var diagnostics = new List<Diagnostic>();

            foreach (var project in projects)
            {
                var compilationWithAnalyzers = project
                    .GetCompilationAsync()
                    .GetAwaiter()
                    .GetResult()
                    .WithAnalyzers(ImmutableArray.Create(analyzer));

                compilationWithAnalyzers
                    .GetAnalyzerDiagnosticsAsync()
                    .GetAwaiter()
                    .GetResult()
                    .ToList()
                    .ForEach(diagnostic =>
                    {
                        if (diagnostic.Location == Location.None || diagnostic.Location.IsInMetadata)
                        {
                            diagnostics.Add(diagnostic);
                        }
                        else
                        {
                            for (int i = 0; i < documents.Length; i++)
                            {
                                var document = documents[i];
                                var tree = document.GetSyntaxTreeAsync().GetAwaiter().GetResult();
                                if (tree == diagnostic.Location.SourceTree)
                                {
                                    diagnostics.Add(diagnostic);
                                }
                            }
                        }
                    });
            }

            return diagnostics
                .OrderBy(d => ExtractIntOrDefault(d.Location.GetLineSpan().Path ?? string.Empty, -1))
                .ThenBy(d => d.Location.SourceSpan.Start)
                .ToArray();
        }

        /// <summary>
        /// Converts the specified <paramref name="sources"/> into source code <see cref="Document"/>s.
        /// </summary>
        /// <param name="sources">
        /// Each <c>string</c> will be used as content for a <see cref="Document"/>.
        /// </param>
        /// <returns>
        /// The specified <paramref name="sources"/> converted into a source code <see cref="Document"/>.
        /// </returns>
        private static Document[] GetDocuments(string[] sources)
        {
            var project = CreateProject(sources);
            var documents = project.Documents.ToArray();

            if (sources.Length != documents.Length)
            {
                throw new InvalidOperationException("Amount of sources did not match amount of Documents created");
            }

            return documents;
        }

        /// <summary>
        /// Create a project using the specified <paramref name="sources"/> as content for source files.
        /// </summary>
        /// <param name="sources">
        /// Each <c>string</c> will be used as content for a source file, which will be added to the project.
        /// </param>
        /// <returns>
        /// A <see cref="Project"/> consisting of files created from the specified <paramref name="sources"/>.
        /// </returns>
        private static Project CreateProject(string[] sources)
        {
            var projectId = ProjectId.CreateNewId(debugName: TestProjectName);

            var solution = new AdhocWorkspace()
                .CurrentSolution
                .AddProject(projectId, TestProjectName, TestProjectName, LanguageNames.CSharp)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CSharpSymbolsReference)
                .AddMetadataReference(projectId, CodeAnalysisReference)
                .AddMetadataReference(projectId, NpgsqlReference);

            int count = 0;
            foreach (var source in sources)
            {
                var fileName = TestFilePrefix + count + "." + DefaultFileExt;
                var documentId = DocumentId.CreateNewId(projectId, debugName: fileName);
                solution = solution.AddDocument(documentId, fileName, SourceText.From(source));
                count++;
            }

            return solution.GetProject(projectId);
        }

        /// <summary>
        /// Extracts an <see cref="int"/> from the specified <paramref name="input"/>. If an <see cref="int"/> is not
        /// found returns the <paramref name="defaultValue"/>.
        /// </summary>
        private static int ExtractIntOrDefault(string input, int defaultValue)
        {
            var match = Regex.Match(input, @"\d+");
            return match.Success ? int.Parse(match.Value) : defaultValue;
        }
    }
}
