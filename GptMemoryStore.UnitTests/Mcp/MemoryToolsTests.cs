using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using ModelContextProtocol;

using Moq;

using NUnit.Framework;

using GptMemoryStore.Mcp;
using GptMemoryStore.Mcp.Models;
using GptMemoryStore.Service;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.UnitTests.Mcp
{
    [TestFixture]
    public sealed class MemoryToolsTests
    {
        private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);
        private static readonly string[] expectedMemoryProperties =
        [
            "id", "createdDateTime", "updatedDateTime", "content", "source", "confidence"
        ];
        private static readonly string[] expectedOrder =
        [
            "updated-newer-creation", "updated-older-creation", "older-update", "newer-creation", "older-creation"
        ];
        private Mock<IMemoryService> serviceMock = new(MockBehavior.Strict);
        private MemoryTools tools = new();

        [SetUp]
        public void SetUp()
        {
            serviceMock = new(MockBehavior.Strict);
            tools = new MemoryTools();
        }

        [Test]
        public void GivenAnEmptyStore_WhenGettingAll_ThenAnEmptyCollectionAndZeroCountAreReturned()
        {
            serviceMock.Setup(service => service.Get()).Returns([]);

            MemoryCollectionToolResult result = tools.GetAll(serviceMock.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Memories, Is.Empty);
                Assert.That(result.Count, Is.Zero);
            }

            serviceMock.Verify(service => service.Get(), Times.Once);
            serviceMock.VerifyNoOtherCalls();
        }

        [Test]
        public void GivenUnorderedMemories_WhenGettingAll_ThenRestOrderingAndCountArePreserved()
        {
            DateTimeOffset timestamp = new(2012, 9, 5, 10, 30, 0, TimeSpan.Zero);
            GptMemory olderCreation = BuildMemory("older-creation");
            GptMemory newerCreation = BuildMemory("newer-creation");
            newerCreation.CreatedDateTime = timestamp.AddDays(1);
            GptMemory olderUpdate = BuildMemory("older-update");
            olderUpdate.UpdatedDateTime = timestamp.AddDays(2);
            GptMemory updatedOlderCreation = BuildMemory("updated-older-creation");
            updatedOlderCreation.UpdatedDateTime = timestamp.AddDays(3);
            GptMemory updatedNewerCreation = BuildMemory("updated-newer-creation");
            updatedNewerCreation.CreatedDateTime = timestamp.AddDays(1);
            updatedNewerCreation.UpdatedDateTime = timestamp.AddDays(3);
            serviceMock.Setup(service => service.Get()).Returns(
            [
                olderCreation, updatedOlderCreation, newerCreation, updatedNewerCreation, olderUpdate
            ]);

            MemoryCollectionToolResult result = tools.GetAll(serviceMock.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Memories.Select(memory => memory.Id), Is.EqualTo(expectedOrder));
                Assert.That(result.Count, Is.EqualTo(expectedOrder.Length));
            }

            serviceMock.Verify(service => service.Get(), Times.Once);
            serviceMock.VerifyNoOtherCalls();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GivenAMemory_WhenGettingById_ThenEveryFieldIsMapped(bool hasUpdate)
        {
            GptMemory memory = BuildMemory("id-613");

            if (hasUpdate)
            {
                memory.UpdatedDateTime = memory.CreatedDateTime.AddDays(1);
            }

            serviceMock.Setup(service => service.Get(memory.Id)).Returns(memory);

            MemoryToolResult result = tools.Get(memory.Id, serviceMock.Object);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Id, Is.EqualTo(memory.Id));
                Assert.That(result.CreatedDateTime, Is.EqualTo(memory.CreatedDateTime));
                Assert.That(result.UpdatedDateTime, Is.EqualTo(memory.UpdatedDateTime));
                Assert.That(result.Content, Is.EqualTo(memory.Content));
                Assert.That(result.Source, Is.EqualTo(memory.Source));
                Assert.That(result.Confidence, Is.EqualTo(memory.Confidence));
            }

            serviceMock.Verify(service => service.Get(memory.Id), Times.Once);
            serviceMock.VerifyNoOtherCalls();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t\r\n")]
        public void GivenABlankIdentifier_WhenGetting_ThenValidationPreventsServiceCalls(string? id)
        {
            Assert.That(
                () => tools.Get(id!, serviceMock.Object),
                Throws.InstanceOf<ArgumentException>());
            serviceMock.VerifyNoOtherCalls();
        }

        [Test]
        public void GivenAServiceFailure_WhenGetting_ThenTheSdkReceivesTheOriginalException()
        {
            KeyNotFoundException failure = new("Private persistence details.");
            serviceMock.Setup(service => service.Get("id-613")).Throws(failure);

            Assert.That(
                () => tools.Get("id-613", serviceMock.Object),
                Throws.Exception.SameAs(failure).And.Not.InstanceOf<McpException>());
            serviceMock.Verify(service => service.Get("id-613"), Times.Once);
            serviceMock.VerifyNoOtherCalls();
        }

        [Test]
        public void GivenACollectionFailure_WhenGettingAll_ThenTheSdkReceivesTheOriginalException()
        {
            InvalidOperationException failure = new("Private persistence details.");
            serviceMock.Setup(service => service.Get()).Throws(failure);

            Assert.That(
                () => tools.GetAll(serviceMock.Object),
                Throws.Exception.SameAs(failure).And.Not.InstanceOf<McpException>());
            serviceMock.Verify(service => service.Get(), Times.Once);
            serviceMock.VerifyNoOtherCalls();
        }

        [Test]
        public void GivenAMemory_WhenSerialisingTheToolResult_ThenTheIndependentCamelCaseContractIsPreserved()
        {
            GptMemory memory = BuildMemory("id-613");
            serviceMock.Setup(service => service.Get(memory.Id)).Returns(memory);

            MemoryToolResult result = tools.Get(memory.Id, serviceMock.Object);
            JsonElement document = JsonSerializer.SerializeToElement(result, serializerOptions);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(
                    document.EnumerateObject().Select(property => property.Name),
                    Is.EqualTo(expectedMemoryProperties));
                Assert.That(document.GetProperty("id").GetString(), Is.EqualTo(memory.Id));
                Assert.That(document.GetProperty("updatedDateTime").ValueKind, Is.EqualTo(JsonValueKind.Null));
            }
        }

        private static GptMemory BuildMemory(string id) => new()
        {
            Id = id,
            CreatedDateTime = new(2012, 9, 5, 10, 30, 0, TimeSpan.Zero),
            Content = "Solaire of Astora enjoys Dark Souls.",
            Source = "An explicit user request.",
            Confidence = 0.613m
        };
    }
}
