using System;
using System.Globalization;
using System.Linq;

using NUnit.Framework;

using GptMemoryStore.Api.Responses;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.UnitTests.Api.Responses
{
    [TestFixture]
    public sealed class GetMemoriesResponseTests
    {
        [Test]
        public void GivenNoMemories_WhenConstructingDefaultResponse_ThenCountIsZeroAndMemoriesAreNull()
        {
            GetMemoriesResponse response = new();

            Assert.That(response.Count, Is.EqualTo(0));
            Assert.That(response.Memories, Is.Null);
        }

        [Test]
        public void GivenEmptyMemories_WhenConstructing_ThenCountIsZero()
        {
            GetMemoriesResponse response = new([]);

            Assert.That(response.Count, Is.EqualTo(0));
        }

        [Test]
        public void GivenEmptyMemories_WhenConstructing_ThenMemoriesListIsEmpty()
        {
            GetMemoriesResponse response = new([]);

            Assert.That(response.Memories, Is.Empty);
        }

        [Test]
        public void GivenThreeMemories_WhenConstructing_ThenCountIsThree()
        {
            GetMemoriesResponse response = new(
            [
                BuildTestMemory("id-1", "Content 1"),
                BuildTestMemory("id-2", "Content 2"),
                BuildTestMemory("id-3", "Content 3")
            ]);

            Assert.That(response.Count, Is.EqualTo(3));
        }

        [Test]
        public void GivenSingleMemory_WhenConstructing_ThenMemoriesContainsThatMemory()
        {
            GptMemory memory = BuildTestMemory("id-613", "Solaire of Astora likes the sun.");

            GetMemoriesResponse response = new([memory]);

            Assert.That(response.Memories, Has.Exactly(1).Items);
            GetMemoryResponse memoryResponse = response.Memories.Single();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(memoryResponse.Identifier, Is.EqualTo(memory.Id));
                Assert.That(memoryResponse.Content, Is.EqualTo(memory.Content));
                Assert.That(memoryResponse.Source, Is.EqualTo(memory.Source));
                Assert.That(memoryResponse.Confidence, Is.EqualTo(memory.Confidence));
                Assert.That(memoryResponse.CreatedDateTime, Is.EqualTo(memory.CreatedDateTime));
                Assert.That(memoryResponse.UpdatedDateTime, Is.EqualTo(memory.UpdatedDateTime));
            }
        }

        [Test]
        public void GivenSingleMemory_WhenConstructing_ThenCountEqualsMemoriesCount()
        {
            GetMemoriesResponse response = new([BuildTestMemory("id-1", "Content")]);

            Assert.That(response.Count, Is.EqualTo(response.Memories.Count()));
        }

        [Test]
        public void GivenMemoriesWithDifferentUpdatedDateTimes_WhenConstructing_ThenOrderedByUpdatedDateTimeDescending()
        {
            GptMemory earlierUpdated = BuildTestMemory("id-1", "Content 1");
            earlierUpdated.UpdatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T08:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GptMemory laterUpdated = BuildTestMemory("id-2", "Content 2");
            laterUpdated.UpdatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GetMemoriesResponse response = new([earlierUpdated, laterUpdated]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("id-2"));
                Assert.That(response.Memories.Skip(1).First().Identifier, Is.EqualTo("id-1"));
            }
        }

        [Test]
        public void GivenMemoriesWithNullUpdatedDateTimes_WhenConstructing_ThenOrderedByCreatedDateTimeDescending()
        {
            GptMemory earlier = BuildTestMemory("id-1", "Content 1");
            earlier.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T08:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            earlier.UpdatedDateTime = null;

            GptMemory later = BuildTestMemory("id-2", "Content 2");
            later.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            later.UpdatedDateTime = null;

            GetMemoriesResponse response = new([earlier, later]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("id-2"));
                Assert.That(response.Memories.Skip(1).First().Identifier, Is.EqualTo("id-1"));
            }
        }

        [Test]
        public void GivenMixedMemories_WhenConstructing_ThenUpdatedDateTimeTakesPriorityInOrdering()
        {
            GptMemory updatedMemory = BuildTestMemory("updated-id", "Updated content");
            updatedMemory.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T06:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            updatedMemory.UpdatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T12:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GptMemory neverUpdatedMemory = BuildTestMemory("never-updated-id", "Never updated");
            neverUpdatedMemory.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T11:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            neverUpdatedMemory.UpdatedDateTime = null;

            GetMemoriesResponse response = new([neverUpdatedMemory, updatedMemory]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("updated-id"));
                Assert.That(response.Memories.Skip(1).First().Identifier, Is.EqualTo("never-updated-id"));
            }
        }

        [Test]
        public void GivenMemoriesWithSameUpdatedDateTime_WhenConstructing_ThenTiesBrokenByCreatedDateTimeDescending()
        {
            DateTimeOffset sharedUpdatedAt = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GptMemory earlierCreated = BuildTestMemory("id-1", "Content 1");
            earlierCreated.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T07:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            earlierCreated.UpdatedDateTime = sharedUpdatedAt;

            GptMemory laterCreated = BuildTestMemory("id-2", "Content 2");
            laterCreated.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T09:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            laterCreated.UpdatedDateTime = sharedUpdatedAt;

            GetMemoriesResponse response = new([earlierCreated, laterCreated]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("id-2"));
                Assert.That(response.Memories.Skip(1).First().Identifier, Is.EqualTo("id-1"));
            }
        }

        [Test]
        public void GivenMemoriesWithSameNullUpdatedDateTimeAndSameCreatedDateTime_WhenConstructing_ThenAllMemoriesArePresent()
        {
            DateTimeOffset sharedCreatedAt = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GptMemory firstMemory = BuildTestMemory("id-1", "Content 1");
            firstMemory.CreatedDateTime = sharedCreatedAt;

            GptMemory secondMemory = BuildTestMemory("id-2", "Content 2");
            secondMemory.CreatedDateTime = sharedCreatedAt;

            GetMemoriesResponse response = new([firstMemory, secondMemory]);

            Assert.That(response.Memories, Has.Exactly(2).Items);
            string[] expectedIdentifiers = ["id-1", "id-2"];
            Assert.That(
                response.Memories.Select(memory => memory.Identifier),
                Is.EqualTo(expectedIdentifiers));
        }

        [Test]
        public void GivenMemoriesWithAllNullUpdatedDateTimes_WhenConstructing_ThenMostRecentCreatedDateTimeIsFirst()
        {
            GptMemory oldest = BuildTestMemory("id-oldest", "Oldest content");
            oldest.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T06:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            oldest.UpdatedDateTime = null;

            GptMemory middle = BuildTestMemory("id-middle", "Middle content");
            middle.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T08:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            middle.UpdatedDateTime = null;

            GptMemory newest = BuildTestMemory("id-newest", "Newest content");
            newest.CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);
            newest.UpdatedDateTime = null;

            GetMemoriesResponse response = new([oldest, middle, newest]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("id-newest"));
                Assert.That(response.Memories.Skip(1).First().Identifier, Is.EqualTo("id-middle"));
                Assert.That(response.Memories.Skip(2).First().Identifier, Is.EqualTo("id-oldest"));
            }
        }

        [Test]
        public void GivenMemoriesWithAllNonNullUpdatedDateTimes_WhenConstructing_ThenMostRecentlyUpdatedIsFirst()
        {
            GptMemory firstUpdated = BuildTestMemory("id-first", "First content");
            firstUpdated.UpdatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T06:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GptMemory lastUpdated = BuildTestMemory("id-last", "Last content");
            lastUpdated.UpdatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture);

            GetMemoriesResponse response = new([firstUpdated, lastUpdated]);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(response.Memories.First().Identifier, Is.EqualTo("id-last"));
                Assert.That(response.Memories.Skip(1).Single().Identifier, Is.EqualTo("id-first"));
            }
        }

        private static GptMemory BuildTestMemory(string id, string content) => new()
        {
            Id = id,
            CreatedDateTime = DateTimeOffset.Parse(
                "2012-09-05T10:00:00.0000000+00:00",
                CultureInfo.InvariantCulture),
            UpdatedDateTime = null,
            Content = content,
            Source = "nucilandia.ro",
            Confidence = 0.95m
        };
    }
}
