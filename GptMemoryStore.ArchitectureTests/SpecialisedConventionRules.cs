using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GptMemoryStore.ArchitectureTests
{
    internal static class SpecialisedConventionRules
    {
        internal static IEnumerable<string> Check(SyntaxTree tree, SemanticModel model)
        {
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                foreach (string reason in CheckNode(node, model))
                {
                    yield return SourceConventionRules.Describe(node, reason);
                }
            }
        }

        private static IEnumerable<string> CheckNode(SyntaxNode node, SemanticModel model)
        {
            if (node is MethodDeclarationSyntax method && model.GetDeclaredSymbol(method) is IMethodSymbol symbol)
            {
                foreach (ITypeSymbol contractType in symbol.Parameters.Select(parameter => parameter.Type).Append(symbol.ReturnType))
                {
                    if (contractType.SpecialType != SpecialType.System_String &&
                        (contractType is IArrayTypeSymbol || contractType is INamedTypeSymbol named &&
                         named.AllInterfaces.Any(contract => contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)) &&
                        contractType.OriginalDefinition.SpecialType != SpecialType.System_Collections_Generic_IEnumerable_T &&
                        !IsFrameworkCollectionContract(symbol, contractType, model.Compilation))
                    {
                        yield return $"{symbol.Name}: collection parameters and returns must use IEnumerable<T>.";
                    }
                }

                if (symbol.ContainingType.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                    method.Body?.Statements.Count == 1 && method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(invocation =>
                        model.GetSymbolInfo(invocation).Symbol is IMethodSymbol called && called.Name == "ProcessRequest"))
                {
                    yield return "Single-expression ProcessRequest actions require expression bodies.";
                }
            }

            if (node is PropertyDeclarationSyntax property && model.GetDeclaredSymbol(property) is IPropertySymbol propertySymbol &&
                ArchitectureTestSources.GetRole(node.SyntaxTree.FilePath) == "Api" && propertySymbol.Name.Contains("Identifier", StringComparison.Ordinal))
            {
                AttributeData? mapping = propertySymbol.GetAttributes().FirstOrDefault(attribute =>
                    attribute.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonPropertyNameAttribute");
                string expected = propertySymbol.Name.Replace("Identifier", "Id", StringComparison.Ordinal);
                expected = char.ToLowerInvariant(expected[0]) + expected[1..];

                if (mapping is null || mapping.ConstructorArguments.Length != 1 ||
                    !string.Equals(mapping.ConstructorArguments[0].Value as string, expected, StringComparison.Ordinal))
                {
                    yield return $"API property {propertySymbol.Name} requires JsonPropertyName(\"{expected}\").";
                }
            }

            if (node is BaseObjectCreationExpressionSyntax creation && model.GetTypeInfo(creation).Type is INamedTypeSymbol created)
            {
                string name = created.ToDisplayString();

                if (name == "System.Text.Json.JsonSerializerOptions")
                {
                    FieldDeclarationSyntax? field = creation.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();

                    if (field is null || !field.Modifiers.Any(SyntaxKind.StaticKeyword) || !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                    {
                        yield return "Cache serializer options in a static readonly field; runtime hot-path claims still require profiling/review.";
                    }
                }

                if (name == "System.Text.RegularExpressions.Regex" && creation.ArgumentList?.Arguments.Count > 0 &&
                    model.GetConstantValue(creation.ArgumentList.Arguments[0].Expression).HasValue)
                {
                    yield return "Use GeneratedRegex for constant regular-expression patterns.";
                }
            }

            if (node is ClassDeclarationSyntax type && model.GetDeclaredSymbol(type) is INamedTypeSymbol declared)
            {
                if (DerivesFrom(declared, "System.Exception"))
                {
                    foreach (string reason in CheckException(declared))
                    {
                        yield return reason;
                    }
                }

                if (DerivesFrom(declared, "NuciLog.Core.LogInfoKey") &&
                    declared.GetMembers().OfType<IPropertySymbol>().Any(property =>
                        property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (ConstructorDeclarationSyntax constructor in type.Members.OfType<ConstructorDeclarationSyntax>())
                    {
                        if (constructor.Initializer is null || !constructor.Initializer.ArgumentList.Arguments.Any(argument =>
                            argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression)))
                        {
                            yield return "Secret-bearing LogInfoKey constructors must pass true to the sensitive base constructor.";
                        }
                    }
                }
            }

            if (node is IdentifierNameSyntax identifier && model.GetSymbolInfo(identifier).Symbol is INamedTypeSymbol builtIn &&
                builtIn.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
                    SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
                    SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Char or SpecialType.System_String or
                    SpecialType.System_Object or SpecialType.System_Decimal or SpecialType.System_Double or SpecialType.System_Single)
            {
                yield return $"Use the built-in lowercase alias rather than {identifier.Identifier.ValueText}.";
            }
        }

        private static bool IsFrameworkCollectionContract(
            IMethodSymbol method,
            ITypeSymbol contractType,
            Compilation compilation)
        {
            // Dependency-injection registration requires the mutable framework service contract.
            if (SymbolEqualityComparer.Default.Equals(contractType,
                compilation.GetTypeByMetadataName("Microsoft.Extensions.DependencyInjection.IServiceCollection")))
            {
                return true;
            }

            if (contractType is not IArrayTypeSymbol { Rank: 1, ElementType.SpecialType: SpecialType.System_String } ||
                !method.IsStatic || method.Parameters.Length != 1 ||
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contractType) ||
                !SymbolEqualityComparer.Default.Equals(method.ContainingType,
                    compilation.GetTypeByMetadataName("GptMemoryStore.Program")))
            {
                return false;
            }

            // The CLI entry point and ASP.NET host discovery require their exact string-array signatures.
            return
                method.Name == "Main" && method.ReturnsVoid ||
                method.Name == "CreateHostBuilder" && method.DeclaredAccessibility == Accessibility.Public &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType,
                    compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostBuilder"));
        }

        private static bool DerivesFrom(INamedTypeSymbol type, string expected)
        {
            INamedTypeSymbol? current = type.BaseType;

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

        private static IEnumerable<string> CheckException(INamedTypeSymbol exception)
        {
            if (!exception.Name.EndsWith("Exception", StringComparison.Ordinal))
            {
                yield return "Custom exception names must end with Exception.";
            }

            foreach (IMethodSymbol constructor in exception.InstanceConstructors.Where(constructor => !constructor.IsImplicitlyDeclared))
            {
                if (constructor.Parameters.Any(parameter => DerivesFromOrIsException(parameter.Type)))
                {
                    continue;
                }

                if (!exception.InstanceConstructors.Any(candidate =>
                    candidate.Parameters.Length == constructor.Parameters.Length + 1 &&
                    candidate.Parameters.Take(constructor.Parameters.Length).Select(parameter => parameter.Type)
                        .SequenceEqual(constructor.Parameters.Select(parameter => parameter.Type), SymbolEqualityComparer.Default) &&
                    DerivesFromOrIsException(candidate.Parameters.Last().Type)))
                {
                    yield return $"{constructor.ToDisplayString()} needs a matching innerException overload.";
                }
            }
        }

        private static bool DerivesFromOrIsException(ITypeSymbol type)
            => type is INamedTypeSymbol named &&
                (string.Equals(named.ToDisplayString(), "System.Exception", StringComparison.Ordinal) || DerivesFrom(named, "System.Exception"));
    }
}