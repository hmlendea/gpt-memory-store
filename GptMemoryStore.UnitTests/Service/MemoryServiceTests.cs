using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Moq;

using NuciDAL.Repositories;

using NuciLog.Core;

using NUnit.Framework;

using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.UnitTests.Service
{
    [TestFixture]
    public sealed class MemoryServiceTests
    {
        private Mock<IFileRepository<GptMemoryDataObject>> repositoryMock = new();
        private Mock<ILogger> loggerMock = new();
        private MemoryService service;

        public MemoryServiceTests()
        {
            service = new(repositoryMock.Object, loggerMock.Object);
        }

        [SetUp]
        public void SetUp()
        {
            repositoryMock = new();
            loggerMock = new();
            service = new(repositoryMock.Object, loggerMock.Object);
        }

        [Test]
        public void GivenValidMemory_WhenCreating_ThenRepositoryAddIsCalledWithCorrectData()
        {
            GptMemory memory = BuildTestMemory();

            service.Create(memory);

            repositoryMock.Verify(
                repository => repository.Add(It.Is<GptMemoryDataObject>(dataObject =>
                    Equals(dataObject.Id, memory.Id) &&
                    Equals(dataObject.Content, memory.Content) &&
                    Equals(dataObject.Source, memory.Source) &&
                    Equals(dataObject.Confidence, memory.Confidence) &&
                    Equals(dataObject.CreatedTimestamp, "2012-09-05T10:30:00.0000000+00:00") &&
                    Equals(dataObject.UpdatedTimestamp, null))),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenCreating_ThenRepositorySaveChangesIsCalled()
        {
            service.Create(BuildTestMemory());

            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenCreating_ThenCreatedTimestampIsSetInDataObject()
        {
            GptMemory memory = BuildTestMemory();

            service.Create(memory);

            repositoryMock.Verify(
                repository => repository.Add(It.Is<GptMemoryDataObject>(dataObject =>
                    Equals(dataObject.CreatedTimestamp, "2012-09-05T10:30:00.0000000+00:00"))),
                Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnAdd_WhenCreating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.Add(It.IsAny<GptMemoryDataObject>()))
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Create(BuildTestMemory()),
                Throws.TypeOf<InvalidOperationException>());
            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Never);
        }

        [Test]
        public void GivenRepositoryThrowsOnSaveChanges_WhenCreating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.SaveChanges())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Create(BuildTestMemory()),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenIdIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Id, Is.EqualTo(dataObject.Id));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenContentIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Content, Is.EqualTo(dataObject.Content));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenSourceIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Source, Is.EqualTo(dataObject.Source));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenConfidenceIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.Confidence, Is.EqualTo(dataObject.Confidence));
        }

        [Test]
        public void GivenMemoryExists_WhenGettingById_ThenCreatedDateTimeIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            DateTimeOffset expectedCreatedDateTime = DateTimeOffset.Parse(
                dataObject.CreatedTimestamp,
                CultureInfo.InvariantCulture);
            Assert.That(result.CreatedDateTime, Is.EqualTo(expectedCreatedDateTime));
        }

        [Test]
        public void GivenMemoryWithNullUpdatedTimestamp_WhenGettingById_ThenUpdatedDateTimeIsNull()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            Assert.That(result.UpdatedDateTime, Is.Null);
        }

        [Test]
        public void GivenMemoryWithUpdatedTimestamp_WhenGettingById_ThenUpdatedDateTimeIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            dataObject.UpdatedTimestamp = "2012-09-05T12:00:00.0000000+00:00";
            repositoryMock.Setup(repository => repository.Get(dataObject.Id)).Returns(dataObject);

            GptMemory result = service.Get(dataObject.Id);

            DateTimeOffset expectedUpdatedDateTime = DateTimeOffset.Parse(
                dataObject.UpdatedTimestamp,
                CultureInfo.InvariantCulture);
            Assert.That(result.UpdatedDateTime, Is.EqualTo(expectedUpdatedDateTime));
        }

        [Test]
        public void GivenRepositoryThrowsOnGet_WhenGettingById_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.Get(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Get("non-existent-id"),
                Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void GivenThreeMemoriesExist_WhenGettingAll_ThenThreeMemoriesAreReturned()
        {
            repositoryMock.Setup(repository => repository.GetAll()).Returns(
            [
                BuildTestDataObject("id-613", "Ilarion Pintilie content"),
                BuildTestDataObject("id-873", "Vasile Ciupitu content"),
                BuildTestDataObject("id-1", "Solaire of Astora content")
            ]);

            IEnumerable<GptMemory> result = service.Get();

            string[] expectedIdentifiers = ["id-613", "id-873", "id-1"];
            Assert.That(
                result.Select(memory => memory.Id),
                Is.EqualTo(expectedIdentifiers));
        }

        [Test]
        public void GivenMemoriesExist_WhenGettingAll_ThenEachMemoryDataIsMappedCorrectly()
        {
            GptMemoryDataObject dataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.GetAll()).Returns([dataObject]);

            GptMemory result = service.Get().Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Id, Is.EqualTo(dataObject.Id));
                Assert.That(result.Content, Is.EqualTo(dataObject.Content));
                Assert.That(result.Source, Is.EqualTo(dataObject.Source));
                Assert.That(result.Confidence, Is.EqualTo(dataObject.Confidence));
                Assert.That(
                    result.CreatedDateTime,
                    Is.EqualTo(DateTimeOffset.Parse(dataObject.CreatedTimestamp, CultureInfo.InvariantCulture)));
                Assert.That(result.UpdatedDateTime, Is.Null);
            }
        }

        [Test]
        public void GivenNoMemoriesExist_WhenGettingAll_ThenEmptyCollectionIsReturned()
        {
            repositoryMock.Setup(repository => repository.GetAll()).Returns([]);

            IEnumerable<GptMemory> result = service.Get();

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GivenRepositoryThrowsOnGetAll_WhenGettingAll_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.GetAll())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Get(),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenRepositoryUpdateIsCalledWithCorrectData()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(existingDataObject.Id)).Returns(existingDataObject);

            GptMemory updatedMemory = new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content by Vasile Ciupitu",
                Source = "nucilandia.ro",
                Confidence = 0.9m,
                UpdatedDateTime = BuildTestMemory().CreatedDateTime.AddHours(4)
            };

            service.Update(updatedMemory);

            repositoryMock.Verify(
                repository => repository.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    Equals(dataObject.Id, updatedMemory.Id) &&
                    Equals(dataObject.Content, updatedMemory.Content) &&
                    Equals(dataObject.Source, updatedMemory.Source) &&
                    Equals(dataObject.Confidence, updatedMemory.Confidence) &&
                    Equals(dataObject.CreatedTimestamp, existingDataObject.CreatedTimestamp) &&
                    Equals(dataObject.UpdatedTimestamp, "2012-09-05T14:30:00.0000000+00:00"))),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenRepositorySaveChangesIsCalled()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(existingDataObject.Id)).Returns(existingDataObject);

            service.Update(new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m
            });

            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenCreatedDateTimeIsPreservedFromExistingRecord()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            existingDataObject.CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00";
            repositoryMock.Setup(repository => repository.Get(existingDataObject.Id)).Returns(existingDataObject);

            service.Update(new()
            {
                Id = existingDataObject.Id,
                CreatedDateTime = BuildTestMemory().CreatedDateTime.AddDays(4),
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m
            });

            repositoryMock.Verify(
                repository => repository.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    Equals(dataObject.CreatedTimestamp, "2012-09-05T10:30:00.0000000+00:00"))),
                Times.Once);
        }

        [Test]
        public void GivenValidMemory_WhenUpdating_ThenUpdatedTimestampIsSetInDataObject()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(existingDataObject.Id)).Returns(existingDataObject);

            DateTimeOffset updatedAt = BuildTestMemory().CreatedDateTime.AddHours(4);

            service.Update(new()
            {
                Id = existingDataObject.Id,
                Content = "Updated content",
                Source = "nucilandia.ro",
                Confidence = 0.9m,
                UpdatedDateTime = updatedAt
            });

            repositoryMock.Verify(
                repository => repository.Update(It.Is<GptMemoryDataObject>(dataObject =>
                    Equals(dataObject.UpdatedTimestamp, "2012-09-05T14:30:00.0000000+00:00"))),
                Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnUpdate_WhenUpdating_ThenExceptionIsRethrown()
        {
            GptMemoryDataObject existingDataObject = BuildTestDataObject();
            repositoryMock.Setup(repository => repository.Get(existingDataObject.Id)).Returns(existingDataObject);
            repositoryMock
                .Setup(repository => repository.Update(It.IsAny<GptMemoryDataObject>()))
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
            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Never);
        }

        [Test]
        public void GivenRepositoryThrowsOnGetDuringUpdate_WhenUpdating_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.Get(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Update(new() { Id = "non-existent-id" }),
                Throws.TypeOf<KeyNotFoundException>());
            repositoryMock.Verify(repository => repository.Update(It.IsAny<GptMemoryDataObject>()), Times.Never);
            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Never);
        }

        [Test]
        public void GivenValidId_WhenDeleting_ThenRepositoryRemoveIsCalledWithCorrectId()
        {
            service.Delete("test-memory-id");

            repositoryMock.Verify(repository => repository.Remove("test-memory-id"), Times.Once);
        }

        [Test]
        public void GivenValidId_WhenDeleting_ThenRepositorySaveChangesIsCalled()
        {
            service.Delete("test-memory-id");

            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Once);
        }

        [Test]
        public void GivenRepositoryThrowsOnRemove_WhenDeleting_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.Remove(It.IsAny<string>()))
                .Throws<KeyNotFoundException>();

            Assert.That(
                () => service.Delete("non-existent-id"),
                Throws.TypeOf<KeyNotFoundException>());
            repositoryMock.Verify(repository => repository.SaveChanges(), Times.Never);
        }

        [Test]
        public void GivenRepositoryThrowsOnSaveChangesAfterRemove_WhenDeleting_ThenExceptionIsRethrown()
        {
            repositoryMock
                .Setup(repository => repository.SaveChanges())
                .Throws<InvalidOperationException>();

            Assert.That(
                () => service.Delete("test-memory-id"),
                Throws.TypeOf<InvalidOperationException>());
        }

        private static GptMemory BuildTestMemory() => new()
        {
            Id = "test-memory-id",
            Content = "Solaire of Astora likes the sun.",
            Source = "nucilandia.ro",
            Confidence = 0.95m,
            CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:30:00.0000000+00:00",
                CultureInfo.InvariantCulture)
        };

        private static GptMemoryDataObject BuildTestDataObject()
            => BuildTestDataObject("test-memory-id", "Solaire of Astora likes the sun.");

        private static GptMemoryDataObject BuildTestDataObject(
            string id,
            string content) => new()
        {
            Id = id,
            CreatedTimestamp = "2012-09-05T10:30:00.0000000+00:00",
            Content = content,
            Source = "nucilandia.ro",
            Confidence = 0.95m
        };
    }
}
