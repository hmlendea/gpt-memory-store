using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GptMemoryStore.ArchitectureTests
{
    internal static class SourceConventionRules
    {
        internal static IEnumerable<string> Check(
            SyntaxTree tree,
            SemanticModel model,
            string projectName)
        {
            SyntaxNode root = tree.GetRoot();

            foreach (SyntaxNode node in root.DescendantNodes(descendIntoTrivia: true))
            {
                foreach (string reason in CheckNode(node, model))
                {
                    yield return Describe(node, reason);
                }
            }

            foreach (string reason in CheckNamespace(root, tree.FilePath, projectName))
            {
                yield return $"{tree.FilePath}: {reason}";
            }

            foreach (string violation in CheckLayout(tree))
            {
                yield return violation;
            }
        }

        internal static string Describe(SyntaxNode node, string reason)
            => $"{node.SyntaxTree.FilePath}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {reason}";

        private static IEnumerable<string> CheckNode(SyntaxNode node, SemanticModel model)
        {
            if (node is VariableDeclarationSyntax variable && variable.Type.IsVar)
            {
                yield return "Use an explicit local type, not var.";
            }

            if (node is ForEachStatementSyntax iteration && iteration.Type.IsVar)
            {
                yield return "Use an explicit foreach element type.";
            }

            if (node is ConditionalExpressionSyntax)
            {
                yield return "Conditional expressions are prohibited; use explicit control flow.";
            }

            if (node is TupleTypeSyntax or TupleExpressionSyntax)
            {
                yield return "Use a dedicated result type instead of a tuple.";
            }

            if (node is ParameterSyntax parameter)
            {
                if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
                {
                    yield return "ref parameters are prohibited.";
                }

                if (parameter.Default is not null)
                {
                    yield return "Use overloads instead of optional parameters.";
                }
            }

            if (node is ParameterListSyntax parameters && parameters.Parameters.Count > 7)
            {
                yield return "Methods, constructors and lambdas accept at most seven parameters.";
            }

            if (node is RegionDirectiveTriviaSyntax or EndRegionDirectiveTriviaSyntax)
            {
                yield return "Region directives are prohibited.";
            }

            if (node is NullableDirectiveTriviaSyntax directive && directive.SettingToken.IsKind(SyntaxKind.EnableKeyword))
            {
                yield return "Enable nullable references in project configuration, not source directives.";
            }

            if (node is GlobalStatementSyntax)
            {
                yield return "Top-level statements require an explicit Program type and static void Main.";
            }

            if (node is AccessorDeclarationSyntax accessor && accessor.IsKind(SyntaxKind.InitAccessorDeclaration))
            {
                yield return "Use set rather than init accessors.";
            }

            if (node is FieldDeclarationSyntax field && field.Modifiers.Any(SyntaxKind.ConstKeyword))
            {
                yield return "Use a static expression-bodied property instead of const.";
            }

            if (node is BinaryExpressionSyntax binary)
            {
                if (binary.IsKind(SyntaxKind.CoalesceExpression))
                {
                    yield return "The ?? operator is prohibited; ??= remains permitted.";
                }

                if ((binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression)) &&
                    (binary.Left.IsKind(SyntaxKind.NullLiteralExpression) || binary.Right.IsKind(SyntaxKind.NullLiteralExpression)))
                {
                    yield return "Use is null or is not null rather than equality with null.";
                }
            }

            if (node is ThrowStatementSyntax throwing && throwing.Expression is IdentifierNameSyntax thrownName &&
                throwing.Ancestors().OfType<CatchClauseSyntax>().Any(catching =>
                    catching.Declaration is not null &&
                    string.Equals(catching.Declaration.Identifier.ValueText, thrownName.Identifier.ValueText, StringComparison.Ordinal)))
            {
                yield return "Use a bare throw to preserve the caught exception stack trace.";
            }

            if (node is MemberDeclarationSyntax member)
            {
                foreach (string reason in CheckMember(member, model))
                {
                    yield return reason;
                }
            }

            if (node is StatementSyntax statement && GetControlledBody(statement) is StatementSyntax body && body is not BlockSyntax)
            {
                yield return "Every control-flow body requires braces.";
            }

            if (node is ElseClauseSyntax alternative && alternative.Statement is not BlockSyntax and not IfStatementSyntax)
            {
                yield return "An else body requires braces.";
            }

            if (node is InvocationExpressionSyntax invocation && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method)
            {
                foreach (string reason in CheckInvocation(invocation, method, model))
                {
                    yield return reason;
                }
            }

            if (node is ObjectCreationExpressionSyntax creation && creation.Parent is EqualsValueClauseSyntax value &&
                value.Parent is VariableDeclaratorSyntax declared && declared.Parent is VariableDeclarationSyntax declaration &&
                !declaration.Type.IsVar && SymbolEqualityComparer.Default.Equals(
                    model.GetTypeInfo(declaration.Type).Type, model.GetTypeInfo(creation).Type))
            {
                yield return "Use target-typed new when the declaration already supplies the type.";
            }

            if (node is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
            {
                yield return "Use a collection expression for inline array initialisation.";
            }
        }

        private static IEnumerable<string> CheckMember(
            MemberDeclarationSyntax member,
            SemanticModel model)
        {
            ISymbol? symbol = model.GetDeclaredSymbol(member);

            if (symbol is null && member is FieldDeclarationSyntax field)
            {
                symbol = model.GetDeclaredSymbol(field.Declaration.Variables.First());
            }

            if (symbol is null || member is BaseNamespaceDeclarationSyntax)
            {
                yield break;
            }

            bool isInterfaceMember = member.Parent is InterfaceDeclarationSyntax;

            if (!isInterfaceMember && member is not EnumMemberDeclarationSyntax &&
                !member.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword) ||
                    modifier.IsKind(SyntaxKind.InternalKeyword) || modifier.IsKind(SyntaxKind.PrivateKeyword) ||
                    modifier.IsKind(SyntaxKind.ProtectedKeyword)))
            {
                yield return $"{symbol.Name} requires explicit accessibility.";
            }

            if ((symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal ||
                 symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary } or INamedTypeSymbol) &&
                !char.IsUpper(symbol.Name.TrimStart('@')[0]) && !symbol.Name.StartsWith(".", StringComparison.Ordinal))
            {
                yield return $"{symbol.Name} must begin with an uppercase letter.";
            }

            if (symbol is IFieldSymbol { DeclaredAccessibility: Accessibility.Private } &&
                (!char.IsLower(symbol.Name[0]) || symbol.Name.StartsWith('_')))
            {
                yield return $"Private field {symbol.Name} must use camelCase without an underscore prefix.";
            }

            if (member is InterfaceDeclarationSyntax contract &&
                (contract.Identifier.ValueText.Length < 2 || !contract.Identifier.ValueText.StartsWith('I') ||
                 !char.IsUpper(contract.Identifier.ValueText[1])))
            {
                yield return "Interfaces require an I-prefixed PascalCase identifier.";
            }

            if (member is MethodDeclarationSyntax method)
            {
                if (method.Body is BlockSyntax { Statements.Count: 1 })
                {
                    StatementSyntax statement = method.Body.Statements[0];

                    if (statement is ReturnStatementSyntax { Expression: not null } or ExpressionStatementSyntax or ThrowStatementSyntax { Expression: not null })
                    {
                        yield return "Single-expression methods must use an expression body.";
                    }
                }

                if (symbol is IMethodSymbol implementation)
                {
                    foreach (INamedTypeSymbol implementedInterface in implementation.ContainingType.AllInterfaces)
                    {
                        foreach (IMethodSymbol interfaceMethod in implementedInterface.GetMembers().OfType<IMethodSymbol>())
                        {
                            if (SymbolEqualityComparer.Default.Equals(
                                implementation.ContainingType.FindImplementationForInterfaceMember(interfaceMethod), implementation) &&
                                !implementation.Parameters.Select(parameter => parameter.Name)
                                    .SequenceEqual(interfaceMethod.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal))
                            {
                                yield return $"Parameter names must match the contract {interfaceMethod.ToDisplayString()}.";
                            }
                        }
                    }
                }
            }

            if (member is ClassDeclarationSyntax type)
            {
                string role = ArchitectureTestSources.GetRole(type.SyntaxTree.FilePath);
                bool isData = role is "Configuration" or "DataAccess" ||
                    type.SyntaxTree.FilePath.Contains($"{Path.DirectorySeparatorChar}Models{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    role is "Api" && type.Identifier.ValueText.EndsWith("Request", StringComparison.Ordinal) ||
                    role is "Api" && type.Identifier.ValueText.EndsWith("Response", StringComparison.Ordinal);
                INamedTypeSymbol? typeSymbol = symbol as INamedTypeSymbol;
                bool isInherited = typeSymbol is not null && ArchitectureTestSources.Compilations.Values
                    .SelectMany(compilation => compilation.SyntaxTrees.SelectMany(tree => tree.GetRoot().DescendantNodes()
                        .OfType<ClassDeclarationSyntax>().Select(candidate => compilation.GetSemanticModel(tree).GetDeclaredSymbol(candidate))))
                    .Any(candidate => candidate is not null && SymbolEqualityComparer.Default.Equals(candidate.BaseType, typeSymbol));

                if (!type.Modifiers.Any(SyntaxKind.StaticKeyword) && !type.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                    !type.Modifiers.Any(SyntaxKind.SealedKeyword) && !isInherited)
                {
                    yield return $"{type.Identifier.ValueText} must be sealed or explicitly designed for inheritance.";
                }

                if (isData && (!type.Modifiers.Any(SyntaxKind.PublicKeyword) || !type.Modifiers.Any(SyntaxKind.SealedKeyword)))
                {
                    yield return "Model, data-object, contract and settings classes must be public sealed.";
                }

                if (type.Modifiers.Any(SyntaxKind.PartialKeyword) &&
                    !type.DescendantNodes().OfType<AttributeSyntax>().Any(attribute =>
                        model.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeConstructor &&
                        string.Equals(attributeConstructor.ContainingType.ToDisplayString(), "System.Text.RegularExpressions.GeneratedRegexAttribute", StringComparison.Ordinal)))
                {
                    yield return "Partial types require a documented framework or source-generator exception.";
                }

                if (type.Identifier.ValueText.EndsWith("MappingExtensions", StringComparison.Ordinal))
                {
                    if (!type.Modifiers.Any(SyntaxKind.InternalKeyword) || !type.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        yield return "Mapping extension classes must be explicitly internal static.";
                    }

                    foreach (MethodDeclarationSyntax mapping in type.Members.OfType<MethodDeclarationSyntax>()
                        .Where(method => method.Modifiers.Any(SyntaxKind.InternalKeyword)))
                    {
                        if (!mapping.Modifiers.Any(SyntaxKind.StaticKeyword) ||
                            !mapping.ParameterList.Parameters.Any(parameter => parameter.Modifiers.Any(SyntaxKind.ThisKeyword)))
                        {
                            yield return $"{mapping.Identifier.ValueText} must be an internal static extension method.";
                        }

                        if (mapping.Identifier.ValueText is not ("ToServiceModel" or "ToServiceModels" or "ToDataObject" or "ToDataObjects"))
                        {
                            yield return $"{mapping.Identifier.ValueText} must use the prescribed service-model/data-object conversion name.";
                        }
                    }
                }

                if ((type.Identifier.ValueText.EndsWith("Service", StringComparison.Ordinal) ||
                     type.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal) ||
                     string.Equals(type.Identifier.ValueText, "Startup", StringComparison.Ordinal)) &&
                    type.ParameterList is null && type.Members.OfType<ConstructorDeclarationSyntax>().Any())
                {
                    yield return "Services, controllers and Startup must use primary constructors and their parameters directly.";
                }

                if (isData && type.Members.OfType<PropertyDeclarationSyntax>()
                    .Where(property => property.ExpressionBody is null &&
                        property.AccessorList?.Accessors.All(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) != true)
                    .Any(property =>
                    property.AccessorList is null || property.AccessorList.Accessors.Any(accessor => accessor.Body is not null || accessor.ExpressionBody is not null)))
                {
                    yield return "Models, entities, contracts and settings require auto-properties.";
                }
            }
        }

        private static IEnumerable<string> CheckInvocation(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            SemanticModel model)
        {
            string owner = method.ContainingType.ToDisplayString();

            if ((owner is "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly") &&
                method.Name is "Parse" or "TryParse" or "ParseExact" or "TryParseExact" or "ToString" &&
                !method.Parameters.Any(parameter => parameter.Type.AllInterfaces.Concat([parameter.Type])
                    .Any(type => SymbolEqualityComparer.Default.Equals(
                        type, model.Compilation.GetTypeByMetadataName("System.IFormatProvider")))) &&
                !method.Parameters.Any(parameter => string.Equals(parameter.Name, "format", StringComparison.Ordinal)))
            {
                yield return "Date/time parsing and formatting require an explicit format or format provider.";
            }

            if (owner.StartsWith("NuciLog", StringComparison.Ordinal) && method.Name is "Debug" or "Info" or "Warn" or "Error" or "Fatal" or "Log")
            {
                if (!invocation.ArgumentList.Arguments.Any(argument => model.GetTypeInfo(argument.Expression).Type is ITypeSymbol argumentType &&
                    HasBase(argumentType, "NuciLog.Core.Operation")))
                {
                    yield return "Every NuciLog event must carry an Operation.";
                }

                if (invocation.ArgumentList.Arguments.SelectMany(argument => argument.DescendantNodes())
                    .OfType<MemberAccessExpressionSyntax>().Any(access =>
                        model.GetSymbolInfo(access).Symbol is IPropertySymbol property &&
                        property.Name is "Message" or "StackTrace" && HasBase(property.ContainingType, "System.Exception")))
                {
                    yield return "Pass the exception through its dedicated logging parameter, not its message or stack trace.";
                }
            }
        }

        private static bool HasBase(ITypeSymbol type, string name)
        {
            ITypeSymbol? current = type;

            while (current is not null)
            {
                if (string.Equals(current.ToDisplayString(), name, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        private static StatementSyntax? GetControlledBody(StatementSyntax statement) => statement switch
        {
            IfStatementSyntax conditional => conditional.Statement,
            ForStatementSyntax loop => loop.Statement,
            ForEachStatementSyntax loop => loop.Statement,
            WhileStatementSyntax loop => loop.Statement,
            DoStatementSyntax loop => loop.Statement,
            UsingStatementSyntax scope => scope.Statement,
            LockStatementSyntax scope => scope.Statement,
            _ => null
        };

        private static IEnumerable<string> CheckNamespace(
            SyntaxNode root,
            string filePath,
            string projectName)
        {
            BaseNamespaceDeclarationSyntax[] namespaces = [.. root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>()];

            if (namespaces.Length != 1 || namespaces.Any(declaration => declaration is FileScopedNamespaceDeclarationSyntax))
            {
                yield return "Exactly one explicit block namespace is required.";
                yield break;
            }

            string directory = Path.GetDirectoryName(filePath)!;
            string relative = Path.GetRelativePath(Path.Combine(ArchitectureTestSources.Root, projectName), directory);
            string expected = projectName;

            if (!string.Equals(relative, ".", StringComparison.Ordinal))
            {
                expected += "." + relative.Replace(Path.DirectorySeparatorChar, '.');
            }

            string actual = namespaces[0].Name.ToString();

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                yield return $"Namespace {actual} must match its project/folder: {expected}.";
            }

            if (actual.Split('.').Any(segment => segment is "Interfaces" or "Enums" or "Enumerations"))
            {
                yield return "Interfaces and enumerations must be colocated with their domain, not in generic namespaces.";
            }

            if (root.DescendantNodes().OfType<ClassDeclarationSyntax>().Any(type =>
                type.Identifier.ValueText.EndsWith("MappingExtensions", StringComparison.Ordinal)) &&
                !actual.EndsWith(".Mapping", StringComparison.Ordinal))
            {
                yield return "Mapping extensions belong in .Mapping (explicit user-selected convention).";
            }

            if (namespaces[0].DescendantNodes().OfType<UsingDirectiveSyntax>().Any())
            {
                yield return "Using directives must be outside the namespace.";
            }
        }

        private static IEnumerable<string> CheckLayout(SyntaxTree tree)
        {
            SyntaxNode root = tree.GetRoot();
            string[] lines = [.. tree.GetText().Lines.Select(line => line.ToString())];

            foreach (SyntaxToken token in root.DescendantTokens())
            {
                int line = tree.GetLineSpan(token.Span).StartLinePosition.Line;

                if (token.IsKind(SyntaxKind.OpenBraceToken) && token.Parent is BlockSyntax or BaseTypeDeclarationSyntax or NamespaceDeclarationSyntax &&
                    !string.Equals(lines[line].Trim(), "{", StringComparison.Ordinal))
                {
                    yield return $"{tree.FilePath}:{line + 1}: Opening block braces must occupy their own line.";
                }

                if ((token.IsKind(SyntaxKind.CloseBraceToken) || token.IsKind(SyntaxKind.CloseParenToken) || token.IsKind(SyntaxKind.CloseBracketToken)) &&
                    line > 0 && string.IsNullOrWhiteSpace(lines[line - 1]) &&
                    string.IsNullOrWhiteSpace(lines[line][..tree.GetLineSpan(token.Span).StartLinePosition.Character]))
                {
                    yield return $"{tree.FilePath}:{line + 1}: No blank line is allowed immediately before a closing delimiter.";
                }
            }

            foreach (BaseParameterListSyntax list in root.DescendantNodes().OfType<BaseParameterListSyntax>())
            {
                if (list.Parameters.Count < 2)
                {
                    continue;
                }

                int startLine = tree.GetLineSpan(list.Span).StartLinePosition.Line;
                int endLine = tree.GetLineSpan(list.Span).EndLinePosition.Line;

                if (startLine == endLine && lines[startLine].Length > 96)
                {
                    yield return Describe(list, "Signatures exceeding 96 characters require one parameter per continuation line.");
                }

                if (startLine != endLine && list.Parameters.Any(parameter =>
                    tree.GetLineSpan(parameter.Span).StartLinePosition.Line == startLine))
                {
                    yield return Describe(list, "Do not mix inline and continued parameters.");
                }
            }

            foreach (UsingDirectiveSyntax directive in root.ChildNodes().OfType<UsingDirectiveSyntax>())
            {
                UsingDirectiveSyntax? next = directive.GetNextSiblingUsing();

                if (next is not null && string.Equals(directive.Name?.ToString().Split('.')[0], next.Name?.ToString().Split('.')[0], StringComparison.Ordinal) &&
                    string.Compare(directive.Name?.ToString(), next.Name?.ToString(), StringComparison.Ordinal) > 0)
                {
                    yield return Describe(directive, "Alphabetise using directives within each package group.");
                }
            }
        }

        private static UsingDirectiveSyntax? GetNextSiblingUsing(this UsingDirectiveSyntax directive)
            => directive.Parent?.ChildNodes().OfType<UsingDirectiveSyntax>()
                .SkipWhile(candidate => candidate != directive).Skip(1).FirstOrDefault();
    }
}