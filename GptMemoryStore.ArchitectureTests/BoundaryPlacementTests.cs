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
    public sealed class BoundaryPlacementTests
    {
        [TestCase("Api")]
        [TestCase("Service")]
        [TestCase("DataAccess")]
        [TestCase("Configuration")]
        [TestCase("Logging")]
        public void GivenProductionRoles_WhenCheckingPlacement_ThenBoundariesRemainExplicit(string role)
        {
            CSharpCompilation compilation = ArchitectureTestSources.Compilations["GptMemoryStore"];
            SyntaxTree[] trees = [.. compilation.SyntaxTrees.Where(tree =>
                string.Equals(ArchitectureTestSources.GetRole(tree.FilePath), role, StringComparison.Ordinal))];
            Assert.That(trees, Is.Not.Empty, $"The {role} boundary must select handwritten production files.");
            string[] violations = [.. trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                .SelectMany(type => CheckType(type, compilation.GetSemanticModel(tree), role)))];

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        private static IEnumerable<string> CheckType(
            TypeDeclarationSyntax type,
            SemanticModel model,
            string role)
        {
            INamedTypeSymbol symbol = model.GetDeclaredSymbol(type)!;
            string name = symbol.Name;
            string space = symbol.ContainingNamespace.ToDisplayString();
            bool isDomain = string.Equals(space, "GptMemoryStore.Service.Models", StringComparison.Ordinal);
            bool isController = HasBase(symbol, "Microsoft.AspNetCore.Mvc.ControllerBase");

            if (role == "Api")
            {
                if (symbol.DeclaredAccessibility == Accessibility.Public &&
                    (name.EndsWith("Repository", StringComparison.Ordinal) || name.EndsWith("Handler", StringComparison.Ordinal)))
                {
                    yield return SourceConventionRules.Describe(type, "API must not expose repositories or command/query handlers.");
                }

                string expected = space;

                if (isController)
                {
                    expected = "GptMemoryStore.Api.Controllers";

                    if (!string.Equals(name, "MemoriesController", StringComparison.Ordinal))
                    {
                        yield return SourceConventionRules.Describe(type, "Controller pluralisation needs an explicit domain mapping; registered mapping: Memory -> MemoriesController.");
                    }

                    foreach (IMethodSymbol action in symbol.GetMembers().OfType<IMethodSymbol>()
                        .Where(method => method.MethodKind == MethodKind.Ordinary && method.DeclaredAccessibility == Accessibility.Public))
                    {
                        if (!string.Equals(action.ReturnType.ToDisplayString(), "Microsoft.AspNetCore.Mvc.ActionResult", StringComparison.Ordinal))
                        {
                            yield return SourceConventionRules.Describe(type, $"Action {action.Name} must return non-generic ActionResult.");
                        }

                        foreach (IParameterSymbol parameter in action.Parameters)
                        {
                            if (!parameter.GetAttributes().Any(attribute => attribute.AttributeClass?.AllInterfaces.Any(contract =>
                                contract.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal) &&
                                contract.Name.StartsWith("I", StringComparison.Ordinal) && contract.Name.EndsWith("Metadata", StringComparison.Ordinal)) == true))
                            {
                                yield return SourceConventionRules.Describe(type, $"Action {action.Name} parameter {parameter.Name} requires an explicit MVC input-binding attribute.");
                            }
                        }
                    }
                }
                else if (name.EndsWith("Request", StringComparison.Ordinal))
                {
                    expected = "GptMemoryStore.Api.Requests";
                }
                else if (name.EndsWith("Response", StringComparison.Ordinal))
                {
                    expected = "GptMemoryStore.Api.Responses";
                }

                if (!string.Equals(expected, space, StringComparison.Ordinal))
                {
                    yield return SourceConventionRules.Describe(type, $"API contract {name} must reside in {expected}.");
                }
            }

            if (role == "Service" && (isController || name.EndsWith("Middleware", StringComparison.Ordinal)))
            {
                yield return SourceConventionRules.Describe(type, "Application services cannot expose controllers or middleware.");
            }

            if (isDomain)
            {
                if (symbol.DeclaredAccessibility == Accessibility.Public &&
                    (isController || name.EndsWith("Handler", StringComparison.Ordinal) || name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith("Request", StringComparison.Ordinal) || name.EndsWith("Response", StringComparison.Ordinal) ||
                     name.EndsWith("Command", StringComparison.Ordinal) || name.EndsWith("Query", StringComparison.Ordinal) ||
                     name.EndsWith("Repository", StringComparison.Ordinal) || name.EndsWith("Validator", StringComparison.Ordinal)))
                {
                    yield return SourceConventionRules.Describe(type, "Domain models must not expose transport, validation, use-case or persistence contracts.");
                }

                foreach (AttributeSyntax attribute in type.DescendantNodes().OfType<AttributeSyntax>())
                {
                    if (model.GetSymbolInfo(attribute).Symbol is IMethodSymbol constructor &&
                        (constructor.ContainingNamespace.ToDisplayString().StartsWith("System.ComponentModel.DataAnnotations.Schema", StringComparison.Ordinal) ||
                         constructor.ContainingNamespace.ToDisplayString().StartsWith("Amazon.DynamoDBv2", StringComparison.Ordinal) ||
                         constructor.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)))
                    {
                        yield return SourceConventionRules.Describe(attribute, "Domain models must not carry persistence attributes.");
                    }
                }
            }

            if (role == "DataAccess" && !name.EndsWith("DataObject", StringComparison.Ordinal))
            {
                yield return SourceConventionRules.Describe(type, "Persisted entity types require the DataObject suffix.");
            }

            if (role == "Configuration" && !name.EndsWith("Settings", StringComparison.Ordinal))
            {
                yield return SourceConventionRules.Describe(type, "Configuration classes require the Settings suffix.");
            }

            if ((HasBase(symbol, "NuciLog.Core.Operation") || HasBase(symbol, "NuciLog.Core.LogInfoKey")) &&
                !string.Equals(space, "GptMemoryStore.Logging", StringComparison.Ordinal))
            {
                yield return SourceConventionRules.Describe(type, "Custom NuciLog types must reside in GptMemoryStore.Logging.");
            }
        }

        private static bool HasBase(INamedTypeSymbol symbol, string expected)
        {
            INamedTypeSymbol? current = symbol;

            while (current is not null)
            {
                if (string.Equals(current.ToDisplayString(), expected, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }
    }
}