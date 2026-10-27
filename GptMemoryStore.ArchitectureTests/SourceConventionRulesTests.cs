using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

namespace GptMemoryStore.ArchitectureTests
{
    [TestFixture]
    public sealed class SourceConventionRulesTests
    {
        [TestCase("\nCall();", 0)]
        [TestCase("Call(\n\n);", 1)]
        [TestCase("Call(\r\n\r\n    );", 1)]
        [TestCase("Call(\n);", 0)]
        [TestCase("\nint[] values = [];", 0)]
        [TestCase("int[] values = [\n\n];", 1)]
        [TestCase("if (true)\n{\n\n}", 1)]
        public void GivenDelimiters_WhenCheckingLayout_ThenOnlyStandaloneClosersRejectBlankLines(
            string body,
            int expectedCount)
        {
            string source = "namespace GptMemoryStore { public static class Memory { " +
                "public static void Run()\n{\n" + body + "\n}\n}\n}";
            string[] violations = [.. CheckSource(source, "Memory.cs")
                .Where(reason => reason.Contains("No blank line", StringComparison.Ordinal))];

            Assert.That(violations, Has.Length.EqualTo(expectedCount));
        }

        [TestCase("public int Count => 42;", 0)]
        [TestCase("public int Count { get; set; }", 0)]
        [TestCase("public int Count { get { return 42; } }", 0)]
        [TestCase("public int Count { get { if (true) { return 0; } return 42; } }", 0)]
        [TestCase("public int Count { get => 42; set { } }", 1)]
        public void GivenResponseProperties_WhenCheckingMembers_ThenComputedReadOnlyPropertiesAreAllowed(
            string property,
            int expectedCount)
        {
            string source = "namespace GptMemoryStore.Api.Responses { public sealed class GetMemoryResponse { " +
                property + " } }";
            string[] violations = [.. CheckSource(source, Path.Combine("Api", "Responses", "GetMemoryResponse.cs"))
                .Where(reason => reason.Contains("auto-properties", StringComparison.Ordinal))];

            Assert.That(violations, Has.Length.EqualTo(expectedCount));
        }

        [TestCase("DateTime.Parse(value, provider)", 0)]
        [TestCase("DateTimeOffset.Parse(value, provider)", 0)]
        [TestCase("DateOnly.Parse(value, provider)", 0)]
        [TestCase("TimeOnly.Parse(value, provider)", 0)]
        [TestCase("DateTime.Parse(value)", 1)]
        public void GivenDateParsing_WhenCheckingOverloads_ThenNullableFormatProvidersAreRecognised(
            string expression,
            int expectedCount)
        {
            string source = "using System; namespace GptMemoryStore { public static class Memory { " +
                "public static object Parse(string value, IFormatProvider? provider) => " + expression + "; } }";
            string[] violations = [.. CheckSource(source, "Memory.cs")
                .Where(reason => reason.Contains("Date/time", StringComparison.Ordinal))];

            Assert.That(violations, Has.Length.EqualTo(expectedCount));
        }

        private static IEnumerable<string> CheckSource(string source, string relativePath)
        {
            string path = Path.Combine(ArchitectureTestSources.Root, "GptMemoryStore", relativePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: path);
            CSharpCompilation compilation = ArchitectureTestSources.Compilations["GptMemoryStore"]
                .RemoveAllSyntaxTrees().AddSyntaxTrees(tree);

            Assert.That(compilation.GetDiagnostics().Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS0103"), Is.Empty);

            return [.. SourceConventionRules.Check(tree, compilation.GetSemanticModel(tree), "GptMemoryStore")];
        }
    }
}