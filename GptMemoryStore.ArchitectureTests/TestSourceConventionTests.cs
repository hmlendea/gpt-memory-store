using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NUnit.Framework;

namespace GptMemoryStore.ArchitectureTests
{
    [TestFixture]
    public sealed class TestSourceConventionTests
    {
        [TestCase("GptMemoryStore.ArchitectureTests")]
        [TestCase("GptMemoryStore.IntegrationTests")]
        [TestCase("GptMemoryStore.UnitTests")]
        public void GivenTestSources_WhenCheckingFixtures_ThenFrameworkNamingAndAssertionsAreConsistent(string projectName)
        {
            CSharpCompilation compilation = ArchitectureTestSources.Compilations[projectName];
            MethodDeclarationSyntax[] tests = [.. compilation.SyntaxTrees.SelectMany(tree =>
                tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Where(method => HasAttribute(method, "Test", compilation) || HasAttribute(method, "TestCase", compilation) ||
                    HasAttribute(method, "TestCaseSource", compilation))];
            Assert.That(tests, Is.Not.Empty, $"{projectName}: test selector must not be empty.");
            string[] violations = [.. tests.SelectMany(method => CheckTest(method, compilation, projectName))];

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        private static IEnumerable<string> CheckTest(
            MethodDeclarationSyntax method,
            CSharpCompilation compilation,
            string projectName)
        {
            ClassDeclarationSyntax fixture = method.Ancestors().OfType<ClassDeclarationSyntax>().First();
            SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);

            if (!fixture.Identifier.ValueText.EndsWith("Tests", StringComparison.Ordinal) || !HasAttribute(fixture, "TestFixture", compilation))
            {
                yield return SourceConventionRules.Describe(fixture, "NUnit fixtures require a subject-based Tests name and TestFixture attribute.");
            }

            bool isBusinessUnitTest = string.Equals(projectName, "GptMemoryStore.UnitTests", StringComparison.Ordinal);

            if (isBusinessUnitTest && (!method.Identifier.ValueText.StartsWith("Given", StringComparison.Ordinal) ||
                !method.Identifier.ValueText.Contains("_When", StringComparison.Ordinal) ||
                !method.Identifier.ValueText.Contains("_Then", StringComparison.Ordinal)))
            {
                yield return SourceConventionRules.Describe(method, "Business unit tests require Given..._When..._Then... names.");
            }

            InvocationExpressionSyntax[] calls = [.. GetCalls(method, model)];

            if (isBusinessUnitTest && !calls.Any(call => IsAssertion(call, model)) &&
                !method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
                    attribute.ArgumentList?.Arguments.Any(argument => argument.NameEquals?.Name.Identifier.ValueText == "ExpectedResult") == true))
            {
                yield return SourceConventionRules.Describe(method, "Every business unit test needs an assertion, Moq verification, or ExpectedResult, including delegated helpers.");
            }

            foreach (InvocationExpressionSyntax call in calls)
            {
                SemanticModel callModel = compilation.GetSemanticModel(call.SyntaxTree);

                if (callModel.GetSymbolInfo(call).Symbol is not IMethodSymbol symbol)
                {
                    continue;
                }

                if (symbol.ContainingNamespace.ToDisplayString().StartsWith("NUnit.Framework", StringComparison.Ordinal) &&
                    symbol.ContainingType.Name is "Assert" or "ClassicAssert" && symbol.Name != "That" && symbol.Name != "Multiple")
                {
                    yield return SourceConventionRules.Describe(call, "Use NUnit Assert.That constraints, not legacy assertion APIs.");
                }

                if (isBusinessUnitTest && symbol.ContainingNamespace.ToDisplayString().StartsWith("System.Reflection", StringComparison.Ordinal) &&
                    call.ArgumentList.Arguments.Any(argument => argument.ToString().Contains("NonPublic", StringComparison.Ordinal)))
                {
                    yield return SourceConventionRules.Describe(call, "Unit tests must not use reflection to test non-public implementation details.");
                }
            }

            if (isBusinessUnitTest && HasAttribute(method, "TestCase", compilation) &&
                method.AttributeLists.SelectMany(list => list.Attributes).Count(attribute =>
                    model.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor && constructor.ContainingType.Name == "TestCaseAttribute") < 2)
            {
                yield return SourceConventionRules.Describe(method, "Parameterised unit tests require at least two cases.");
            }
        }

        private static bool HasAttribute(
            SyntaxNode node,
            string name,
            CSharpCompilation compilation)
            => node.ChildNodes().OfType<AttributeListSyntax>().SelectMany(list => list.Attributes).Any(attribute =>
                compilation.GetSemanticModel(node.SyntaxTree).GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
                string.Equals(constructor.ContainingType.ToDisplayString(), $"NUnit.Framework.{name}Attribute", StringComparison.Ordinal));

        private static bool IsAssertion(
            InvocationExpressionSyntax invocation,
            SemanticModel model)
        {
            SemanticModel actualModel = model.Compilation.GetSemanticModel(invocation.SyntaxTree);

            return actualModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol symbol &&
                (symbol.ContainingNamespace.ToDisplayString().StartsWith("NUnit.Framework", StringComparison.Ordinal) && symbol.Name == "That" ||
                 symbol.ContainingNamespace.ToDisplayString().StartsWith("Moq", StringComparison.Ordinal) && symbol.Name.StartsWith("Verify", StringComparison.Ordinal));
        }

        private static IEnumerable<InvocationExpressionSyntax> GetCalls(
            MethodDeclarationSyntax method,
            SemanticModel model)
        {
            HashSet<IMethodSymbol> visited = new(SymbolEqualityComparer.Default);

            return Traverse(method, model);

            IEnumerable<InvocationExpressionSyntax> Traverse(
                MethodDeclarationSyntax declaration,
                SemanticModel semanticModel)
            {
                foreach (InvocationExpressionSyntax call in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    yield return call;

                    if (semanticModel.GetSymbolInfo(call).Symbol is not IMethodSymbol symbol || !visited.Add(symbol))
                    {
                        continue;
                    }

                    foreach (MethodDeclarationSyntax helper in symbol.DeclaringSyntaxReferences
                        .Select(reference => reference.GetSyntax()).OfType<MethodDeclarationSyntax>())
                    {
                        foreach (InvocationExpressionSyntax delegated in Traverse(
                            helper, semanticModel.Compilation.GetSemanticModel(helper.SyntaxTree)))
                        {
                            yield return delegated;
                        }
                    }
                }
            }
        }
    }
}