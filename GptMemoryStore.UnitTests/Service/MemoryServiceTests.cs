using System;
using System.Collections.Generic;
using System.Linq;
using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service;
using GptMemoryStore.Service.Models;
using Moq;
using NuciDAL.Repositories;
using NuciLog.Core;
using NUnit.Framework;

namespace GptMemoryStore.UnitTests.Service
{
    [TestFixture]
    public sealed class MemoryServiceTests
    {
        Mock<IFileRepository<GptMemoryDataObject>> repositoryMock;
        Mock<ILogger> loggerMock;
        MemoryService service;

        [SetUp]
        public void SetUp()
        {
            repositoryMock = new Mock<IFileRepository<GptMemoryDataObject>>();
            loggerMock = new Mock<ILogger>();
            service = new MemoryService(repositoryMock.Object, loggerMock.Object);
        }

        // ── Create ──────────────────────────────────────────────────────────────

        [Test]
        public void GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData()
        {
            GptMemory memory = BuildTestMemory();

            service.Create(memory);

            repositoryMock.Verify(
                r => r.Add(It.Is<GptMemoryDataObject>(dataObject =>
                    dataObject.Id == memory.Id &&
                    dataObject.Content == memory.Content &&
                    dataObject.Source == memory.Source &&
                    dataObject.Confidence == memory.Confidence)),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenCreating_ThenRepositorySaveChangesIsCalled()
        {
            service.Create(BuildTestMemory());

            repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenCreating_ThenCreatedTimestampIsSetInDataObject()
        {
            GptMemory memory = BuildTestMemory();

            service.Create(memory);

            repositoryMock.Verify(
                r => r.Add(It.Is<GptMemoryDataObject>(dataObject =>
                    !string.IsNullOrEmpty(dataObject.CreatedTimestamp))),
                Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnAdd_WhenCreating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.Add(It.IsAny<GptMemoryDataObject>()))
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Create(BuildTestMemory()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GivenRepositoryThrowsOnSaveChanges_WhenCreating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.SaveChanges())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Create(BuildTestMemory()),
                Throws.TypeOf<InvalidOperationException>());
        }

        // ── Get(id) ─────────────────────────────────────────────────────────────

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenIdIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Id, Is.EqualTo(dataObject.Id));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenContentIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Content, Is.EqualTo(dataObject.Content));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenSourceIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Source, Is.EqualTo(dataObject.Source));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenConfidenceIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Confidence, Is.EqualTo(dataObject.Confidence));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenCreatedDateTimeIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            DateTimeOffset expectedCreatedDateTime = DateTimeOffset.Parse(dataObject.CreatedTimestamp);
            Assert.That(result.CreatedDateTime, Is.EqualTo(expectedCreatedDateTime));
        }

        [Test]
        public void GivenMemoryWithNullUpdatedTimestamp_WhenGettingById_ThenUpdatedDateTimeIsNull()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            dataObject.UpdatedTimestamp = null;
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.UpdatedDateTime, Is.Null);
        }

        [Test]
        public void GivenMemoryWithUpdatedTimestamp_WhenGettingById_ThenUpdatedDateTimeIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            dataObject.UpdatedTimestamp = "2012-09-05T12:00:00.0000000+00:00";
            repositoryMock.Setup(r => r.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            DateTimeOffset expectedUpdatedDateTime = DateTimeOffset.Parse(dataObject.UpdatedTimestamp);
            Assert.That(result.UpdatedDateTime, Is.EqualTo(expectedUpdatedDateTime));
        }

        [Test]
        public void GivenRepositoryThrowsOnGet_WhenGettingById_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.Get(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Get("non-existent-id"),
                Throws.TypeOf<KeyNotFoundException>());
        }

        // ── Get() ───────────────────────────────────────────────────────────────

        [Test]
        public void GivenThreeMemoriesExist_WhenGettingAll_ThenThreeMemoriesAreReturned()
        {
            repositoryMock.Setup(r => r.GetAll()).Returns(
            [
                BuildTestDataObject("id-613", "Ilarion Pintilie content"),
                BuildTestDataObject("id-873", "Vasile Ciupitu content"),
                BuildTestDataObject("id-1", "Solaire of Astora content")
            ]);

            IEnumerable<GptMemory> result = service.Get();

            Assert.That(result, Has.Exactly(3).Items);
        }

        [Test]
        public void GivenMemoriesExist_WhenGettingAll_ThenEachMemoryDataIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.GetAll()).Returns([dataObject]);

            GptMemory result = service.Get().First();

            Assert.That(result.Id, Is.EqualTo(dataObject.Id));
            Assert.That(result.Content, Is.EqualTo(dataObject.Content));
            Assert.That(result.Source, Is.EqualTo(dataObject.Source));
            Assert.That(result.Confidence, Is.EqualTo(dataObject.Confidence));
        }

        [Test]
        public void GivenNoMemoriesExist_WhenGettingAll_ThenEmptyCollectionIsReturned()
        {
            repositoryMock.Setup(r => r.GetAll()).Returns([]);

            IEnumerable<GptMemory> result = service.Get();

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GivenRepositoryThrowsOnGetAll_WhenGettingAll_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.GetAll())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Get(),
                Throws.TypeOf<InvalidOperationException>());
        }

        // ── Update ──────────────────────────────────────────────────────────────

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenRepositoryUpdateIsCalledWithCorrectData()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(existingDataObject.Id)).Returns(existingDataObject);

            GptMemory updatedMemory = new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content by Vasile Ciupitu",
                Source = "nucilandia.ro",
                Confidence = 0.9m,
                UpdatedDateTime = DateTimeOffset.UtcNow
            };

            service.Update(updatedMemory);

            repositoryMock.Verify(
                r => r.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    dataObject.Id == updatedMemory.Id &&
                    dataObject.Content == updatedMemory.Content &&
                    dataObject.Source == updatedMemory.Source &&
                    dataObject.Confidence == updatedMemory.Confidence)),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenRepositorySaveChangesIsCalled()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(existingDataObject.Id)).Returns(existingDataObject);

            service.Update(new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m
            });

            repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenCreatedDateTimeIsPreservedFromExistingRecord()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            existingDataObject.CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00";
            repositoryMock.Setup(r => r.Get(existingDataObject.Id)).Returns(existingDataObject);

            service.Update(new()
            {
                Id = existingDataObject.Id,
                CreatedDateTime = DateTimeOffset.UtcNow,
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m
            });

            repositoryMock.Verify(
                r => r.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    dataObject.CreatedTimestamp == "2012-09-05T10:30:00.0000000+00:00")),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenUpdatedTimestampIsSetInDataObject()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(existingDataObject.Id)).Returns(existingDataObject);

            DateTimeOffset updatedAt = DateTimeOffset.UtcNow;

            service.Update(new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m,
                UpdatedDateTime = updatedAt
            });

            repositoryMock.Verify(
                r => r.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    dataObject.UpdatedTimestamp != null)),
                Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnUpdate_WhenUpdating_ThenExceptionIsRethrown()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(r => r.Get(existingDataObject.Id)).Returns(existingDataObject);
            repositoryMock
                .Setup(r => r.Update(It.IsAny<GptMemoryDataObject>()))
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Update(new()
                {
                    Id = existingDataObject.Id,
                    Content = "Updated content",
                    Source = "nucilandia.ro",
                    Confidence = 0.9m
                }),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GivenRepositoryThrowsOnGetDuringUpdate_WhenUpdating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.Get(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Update(new() { Id = "non-existent-id" }),
                Throws.TypeOf<KeyNotFoundException>());
        }

        // ── Delete ──────────────────────────────────────────────────────────────

        [Test]
        public void GivenValidId_WhenDeleting_ThenRepositoryRemoveIsCalledWithCorrectId()
        {
            service.Delete("test-memory-id");

            repositoryMock.Verify(r => r.Remove("test-memory-id"), Times.Once);
        }

        [Test]
        public void GivenValidId_WhenDeleting_ThenRepositorySaveChangesIsCalled()
        {
            service.Delete("test-memory-id");

            repositoryMock.Verify(r => r.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnRemove_WhenDeleting_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.Remove(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Delete("non-existent-id"),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GivenRepositoryThrowsOnSaveChangesAfterRemove_WhenDeleting_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(r => r.SaveChanges())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Delete("test-memory-id"),
                Throws.TypeOf<InvalidOperationException>());
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        static GptMemory BuildTestMemory() => new()
        {
            Id = "test-memory-id",
            Content = "Solaire of Astora likes the sun.",
            Source = "nucilandia.ro",
            Confidence = 0.95m,
            CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:30:00.0000000+00:00")
        };

        static GptMemoryDataObject BuildTestDataObject()
            => BuildTestDataObject("test-memory-id", "Solaire of Astora likes the sun.");

        static GptMemoryDataObject BuildTestDataObject(string id, string content) => new()
        {
            Id = id,
            CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00",
            UpdatedTimestamp = null,
            Content = content,
            Source = "nucilandia.ro",
            Confidence = 0.95m
        };
    }
}
