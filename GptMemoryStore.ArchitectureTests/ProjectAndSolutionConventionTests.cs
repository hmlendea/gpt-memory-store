using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

using NUnit.Framework;

namespace GptMemoryStore.ArchitectureTests
{
    [TestFixture]
    public sealed class ProjectAndSolutionConventionTests
    {
        [Test]
        public void GivenTheSolution_WhenCheckingStructure_ThenProjectsUseTheDocumentedLayout()
        {
            string path = Path.Combine(ArchitectureTestSources.Root, "GptMemoryStore.slnx");
            string text = File.ReadAllText(path);
            XDocument document = XDocument.Parse(text);
            XElement solution = document.Root!;

            Assert.That(document.Declaration, Is.Null, "The SDK-style solution must not have an XML declaration.");
            Assert.That(solution.Name.LocalName, Is.EqualTo("Solution"));
            Assert.That(solution.Attributes(), Is.Empty, "The Solution root must not have attributes.");
            Assert.That(solution.Descendants("Version"), Is.Empty);
            Assert.That(Directory.EnumerateFiles(ArchitectureTestSources.Root, "*.sln"), Is.Empty);
            Assert.That(solution.Elements().All(element => element.Name.LocalName == "Project"),
                "The solution must explicitly list its projects without nesting folders.");
            string[] expected = [.. ArchitectureTestSources.ProjectNames.Select(name => $"{name}/{name}.csproj")];
            Assert.That(solution.Elements("Project").Select(project => project.Attribute("Path")?.Value),
                Is.EqualTo(expected), "Main project first, then source projects, then alphabetically ordered tests.");

            foreach (XElement project in solution.Elements("Project"))
            {
                Assert.That(project.Attributes().Select(attribute => attribute.Name.LocalName), Is.EqualTo(new[] { "Path" }));
                Assert.That(project.Elements(), Is.Empty);
                Assert.That(File.Exists(Path.Combine(ArchitectureTestSources.Root, project.Attribute("Path")!.Value)),
                    $"Solution source {project.Attribute("Path")!.Value} must exist in the checkout.");
            }
        }

        [TestCase("GptMemoryStore")]
        [TestCase("GptMemoryStore.ArchitectureTests")]
        [TestCase("GptMemoryStore.IntegrationTests")]
        [TestCase("GptMemoryStore.UnitTests")]
        public void GivenAProject_WhenCheckingDeclarations_ThenRequiredPoliciesAreExplicit(string projectName)
        {
            XDocument project = LoadProject(projectName);
            Dictionary<string, string> required = new()
            {
                ["ImplicitUsings"] = "disable",
                ["LangVersion"] = "latest",
                ["Nullable"] = "enable",
                ["IsPackable"] = "false"
            };

            if (!string.Equals(projectName, "GptMemoryStore", StringComparison.Ordinal))
            {
                required.Add("IsTestProject", "true");
                required.Add("ExcludeFromCodeCoverage", "true");
            }

            foreach (KeyValuePair<string, string> setting in required)
            {
                string[] values = [.. project.Descendants(setting.Key).Select(element => element.Value)];
                Assert.That(values, Is.Not.Empty, $"{projectName}: declare {setting.Key} explicitly.");
                Assert.That(values, Is.All.EqualTo(setting.Value),
                    $"{projectName}: contradictory or missing required declaration {setting.Key}={setting.Value}.");
            }

            string[] packages = [.. project.Descendants("PackageReference").Select(reference => reference.Attribute("Include")?.Value ?? string.Empty)];
            Assert.That(packages, Is.Ordered.Using<string>(StringComparer.Ordinal), $"{projectName}: package references must be alphabetical.");
            Assert.That(project.Descendants("TargetFramework").Any() || project.Descendants("TargetFrameworks").Any(),
                $"{projectName}: declare the target framework explicitly.");

            foreach (string framework in project.Descendants().Where(element =>
                element.Name.LocalName is "TargetFramework" or "TargetFrameworks").SelectMany(element => element.Value.Split(';')))
            {
                AssertFramework(projectName, framework);
            }
        }

        [TestCase("Debug")]
        [TestCase("Release")]
        public void GivenSupportedConfigurations_WhenEvaluatingMsBuild_ThenImportedPropertiesAndReferencesPreserveBoundaries(string configuration)
        {
            foreach (string projectName in ArchitectureTestSources.ProjectNames)
            {
                using JsonDocument metadata = EvaluateProject(projectName, configuration);
                JsonElement properties = metadata.RootElement.GetProperty("Properties");

                foreach (string framework in properties.GetProperty("TargetFramework").GetString()!.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Concat(properties.GetProperty("TargetFrameworks").GetString()!.Split(';', StringSplitOptions.RemoveEmptyEntries)))
                {
                    AssertFramework(projectName, framework);
                }

                Assert.That(properties.GetProperty("TargetFramework").GetString() + properties.GetProperty("TargetFrameworks").GetString(),
                    Is.Not.Empty, $"{projectName}/{configuration}: target framework metadata is missing.");
                Assert.That(properties.GetProperty("ImplicitUsings").GetString(), Is.EqualTo("disable"));
                Assert.That(properties.GetProperty("Nullable").GetString(), Is.EqualTo("enable"));
                Assert.That(properties.GetProperty("LangVersion").GetString(), Is.EqualTo("latest"));
                Assert.That(properties.GetProperty("IsPackable").GetString(), Is.EqualTo("false"),
                    "This repository publishes an application, not a NuGet package.");
                JsonElement items = metadata.RootElement.GetProperty("Items");

                if (string.Equals(projectName, "GptMemoryStore", StringComparison.Ordinal))
                {
                    Assert.That(items.GetProperty("InternalsVisibleTo").GetArrayLength(), Is.Zero,
                        "Production must not declare InternalsVisibleTo, including through imported MSBuild items.");
                    Assert.That(items.GetProperty("ProjectReference").EnumerateArray()
                        .Select(item => item.GetProperty("Identity").GetString()), Is.Empty,
                        "The documented single production project must not reference test or undeclared layer projects.");
                    Assert.That(items.GetProperty("Reference").EnumerateArray()
                        .Select(item => item.GetProperty("Identity").GetString())
                        .Where(name => name is not null && name.Contains("Tests", StringComparison.Ordinal)), Is.Empty,
                        "Production assembly references must never point at tests.");
                }
                else
                {
                    Assert.That(properties.GetProperty("IsTestProject").GetString(), Is.EqualTo("true"));
                    Assert.That(properties.GetProperty("ExcludeFromCodeCoverage").GetString(), Is.EqualTo("true"));
                }
            }
        }

        [Test]
        public void GivenArchitectureTests_WhenCheckingDependencies_ThenRequiredMechanismsAreDeclared()
        {
            XDocument project = LoadProject("GptMemoryStore.ArchitectureTests");
            string[] packages = [.. project.Descendants("PackageReference").Select(reference => reference.Attribute("Include")!.Value)];

            Assert.That(packages, Does.Contain("NUnit"));
            Assert.That(packages, Does.Contain("TngTech.ArchUnitNET.NUnit"));
            Assert.That(packages, Does.Contain("Microsoft.CodeAnalysis.CSharp"));
            Assert.That(project.Descendants("RootNamespace").Single().Value, Is.EqualTo("GptMemoryStore.ArchitectureTests"));
        }

        [Test]
        public void GivenProductionSources_WhenCheckingAssemblyAttributes_ThenInternalsAreNotExposed()
        {
            string[] violations = [.. ArchitectureTestSources.GetTrees("GptMemoryStore")
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax>())
                .Where(attribute => attribute.Name.ToString().Contains("InternalsVisibleTo", StringComparison.Ordinal))
                .Select(attribute => SourceConventionRules.Describe(attribute, "Production cannot declare InternalsVisibleTo."))];

            Assert.That(violations, Is.Empty, string.Join(Environment.NewLine, violations));
        }

        private static XDocument LoadProject(string projectName)
            => XDocument.Load(Path.Combine(ArchitectureTestSources.Root, projectName, $"{projectName}.csproj"));

        private static void AssertFramework(string projectName, string framework)
        {
            string version = framework.Split('-')[0].Replace("net", string.Empty, StringComparison.Ordinal);

            Assert.That(framework.StartsWith("net", StringComparison.Ordinal) && Version.TryParse(version, out Version? parsed) && parsed.Major >= 10,
                $"{projectName}: {framework} must target .NET 10.0 or newer.");
        }

        private static JsonDocument EvaluateProject(string projectName, string configuration)
        {
            ProcessStartInfo start = new("dotnet")
            {
                WorkingDirectory = ArchitectureTestSources.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add("msbuild");
            start.ArgumentList.Add(Path.Combine(projectName, $"{projectName}.csproj"));
            start.ArgumentList.Add($"-property:Configuration={configuration}");
            start.ArgumentList.Add("-getProperty:TargetFramework,TargetFrameworks,ImplicitUsings,Nullable,LangVersion,IsPackable,IsTestProject,ExcludeFromCodeCoverage");
            start.ArgumentList.Add("-getItem:ProjectReference,Reference,InternalsVisibleTo,AssemblyAttribute");
            using Process process = Process.Start(start)!;
            System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
            System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();
            bool completed = process.WaitForExit(60000);

            if (!completed)
            {
                process.Kill(true);
            }

            Assert.That(completed, $"{projectName}/{configuration}: MSBuild evaluation timed out.");
            Assert.That(process.ExitCode, Is.Zero, $"{projectName}/{configuration}: {error.GetAwaiter().GetResult()}");

            return JsonDocument.Parse(output.GetAwaiter().GetResult());
        }
    }
}