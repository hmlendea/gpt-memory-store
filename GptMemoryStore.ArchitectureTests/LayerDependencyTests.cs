using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.NUnit;

using NUnit.Framework;

using GptMemoryStore.Service;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace GptMemoryStore.ArchitectureTests
{
    [TestFixture]
    public sealed class LayerDependencyTests
    {
        private static readonly Architecture architecture = new ArchLoader()
            .LoadAssemblies(typeof(IMemoryService).Assembly)
            .Build();

        [TestCase("Api", "GptMemoryStore\\.DataAccess(\\..*)?")]
        [TestCase("Api", "GptMemoryStore\\.Service\\.Mapping(\\..*)?")]
        [TestCase("Api", "GptMemoryStore\\.Service\\.MemoryService")]
        [TestCase("Service", "GptMemoryStore\\.Api(\\..*)?")]
        [TestCase("Service", "GptMemoryStore\\.Configuration(\\..*)?")]
        [TestCase("Service.Models", "GptMemoryStore\\.(Api|Configuration|DataAccess|Logging)(\\..*)?")]
        [TestCase("Service.Models", "GptMemoryStore\\.Service\\.(Mapping\\..*|MemoryService|IMemoryService)")]
        [TestCase("DataAccess", "GptMemoryStore\\.(Api|Service|Configuration|Logging)(\\..*)?")]
        [TestCase("Configuration", "GptMemoryStore\\.(Api|Service|DataAccess|Logging)(\\..*)?")]
        [TestCase("Logging", "GptMemoryStore\\.(Api|Service|DataAccess|Configuration)(\\..*)?")]
        public void GivenAnArchitecturalArea_WhenCheckingDependencies_ThenForbiddenAreasAreNotReferenced(
            string area,
            string forbiddenTypes)
        {
            IObjectProvider<IType> sourceTypes = Types().That()
                .ResideInNamespaceOrBelow($"GptMemoryStore.{area}");

            Assert.That(
                sourceTypes.GetObjects(architecture),
                Is.Not.Empty,
                $"The {area} boundary must select production types.");

            Types().That().Are(sourceTypes).Should().NotDependOnAny(
                Types().That().HaveFullNameMatching($"^{forbiddenTypes}$"))
                .Because($"The documented {area} boundary prohibits dependencies on {forbiddenTypes}.")
                .Check(architecture);
        }

        [TestCase("Api", "^(NuciDAL|Microsoft\\.EntityFrameworkCore|Amazon\\.DynamoDBv2)(\\..*)?$")]
        [TestCase("Service", "^(NuciDAL\\.Repositories\\.JsonRepository.*|NuciDAL\\.DependencyInjection\\..*|NuciLog\\.NuciLogger|Microsoft\\.AspNetCore\\..*|NuciAPI\\..*|Microsoft\\.EntityFrameworkCore\\..*|Amazon\\.DynamoDBv2\\..*)$")]
        [TestCase("Service.Models", "^(Nuci.*|Microsoft\\.AspNetCore\\..*|Microsoft\\.EntityFrameworkCore\\..*|Amazon\\.DynamoDBv2\\..*|Dapper\\..*|MediatR\\..*)$")]
        public void GivenAnArchitecturalArea_WhenCheckingInfrastructure_ThenOnlyPermittedAbstractionsAreUsed(
            string area,
            string forbiddenTypes)
        {
            IObjectProvider<IType> sourceTypes = Types().That()
                .ResideInNamespaceOrBelow($"GptMemoryStore.{area}");

            Assert.That(
                sourceTypes.GetObjects(architecture),
                Is.Not.Empty,
                $"The {area} infrastructure rule must select production types.");

            Types().That().Are(sourceTypes).Should().NotDependOnAny(
                Types(includeReferenced: true).That().HaveFullNameMatching(forbiddenTypes))
                .Because($"The {area} boundary must not use concrete or unrelated infrastructure.")
                .Check(architecture);
        }

        [TestCase("GptMemoryStore.Api.Controllers", "GptMemoryStore.Service.IMemoryService")]
        [TestCase("GptMemoryStore.Service", "NuciDAL.Repositories.IFileRepository.*")]
        [TestCase("GptMemoryStore.Service", "NuciLog.Core.ILogger")]
        public void GivenAnApplicationBoundary_WhenCheckingCollaborators_ThenItsAbstractionIsUsed(
            string area,
            string collaborator)
        {
            IObjectProvider<Class> sourceTypes = Classes().That().ResideInNamespace(area);

            Assert.That(sourceTypes.GetObjects(architecture), Is.Not.Empty);
            Assert.That(
                Types(includeReferenced: true).That()
                    .HaveFullNameMatching($"^{collaborator}$").GetObjects(architecture),
                Is.Not.Empty,
                $"The required collaborator {collaborator} must be present in the loaded architecture.");

            Types().That().Are(sourceTypes).Should().DependOnAny(
                Types(includeReferenced: true).That().HaveFullNameMatching($"^{collaborator}$"))
                .Because($"The {area} boundary delegates through {collaborator}.")
                .Check(architecture);
        }
    }
}