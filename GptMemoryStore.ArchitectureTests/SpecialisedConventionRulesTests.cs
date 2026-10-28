using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NUnit.Framework;

namespace GptMemoryStore.ArchitectureTests
{
    [TestFixture]
    public sealed class SpecialisedConventionRulesTests
    {
        [TestCase("public static IServiceCollection Register(IServiceCollection services) => services;", 0)]
        [TestCase("public static ServiceCollection Register(ServiceCollection services) => services;", 2)]
        [TestCase("public static IEnumerable<string> GetValues(IEnumerable<string> values) => values;", 0)]
        [TestCase("public static List<string> GetValues(List<string> values) => values;", 2)]
        [TestCase("public static IHostBuilder CreateHostBuilder(string[] arguments) => Host.CreateDefaultBuilder(arguments);", 0)]
        [TestCase("public static IHostBuilder CreateHostBuilder(int[] arguments) => Host.CreateDefaultBuilder();", 1)]
        [TestCase("public static string[] CreateHostBuilder(string[] arguments) => arguments;", 2)]
        [TestCase("public IHostBuilder CreateHostBuilder(string[] arguments) => Host.CreateDefaultBuilder(arguments);", 1)]
        [TestCase("public static IHostBuilder Other(string[] arguments) => Host.CreateDefaultBuilder(arguments);", 1)]
        public void GivenCollectionContracts_WhenCheckingMethods_ThenOnlyFrameworkContractsAreExcepted(
            string method,
            int expectedCount)
        {
            string source = "using System.Collections.Generic; using Microsoft.Extensions.DependencyInjection; " +
                "using Microsoft.Extensions.Hosting; namespace GptMemoryStore { public sealed class Program { " +
                method + " } }";
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            CSharpCompilation compilation = ArchitectureTestSources.Compilations["GptMemoryStore"]
                .RemoveAllSyntaxTrees().AddSyntaxTrees(tree);

            Assert.That(compilation.GetDiagnostics().Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);

            string[] violations = [.. SpecialisedConventionRules.Check(tree, compilation.GetSemanticModel(tree))
                .Where(reason => reason.Contains("collection parameters", StringComparison.Ordinal))];

            Assert.That(violations, Has.Length.EqualTo(expectedCount));
        }
    }
}