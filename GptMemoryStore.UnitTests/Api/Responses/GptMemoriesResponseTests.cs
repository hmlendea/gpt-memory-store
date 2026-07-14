using System;
using System.Collections.Generic;
using GptMemoryStore.Api.Responses;
using GptMemoryStore.Service.Models;
using NUnit.Framework;

namespace GptMemoryStore.UnitTests.Api.Responses
{
    [TestFixture]
    public sealed class GetMemoriesResponseTests
    {
        // ── Constructor / Count ─────────────────────────────────────────────────

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

            Assert.That(response.Memories, Has.Count.EqualTo(1));
            Assert.That(response.Memories[0].Id, Is.EqualTo(memory.Id));
        }

        [Test]
        public void GivenSingleMemory_WhenConstructing_ThenCountEqualsMemoriesCount()
        {
            GetMemoriesResponse response = new([BuildTestMemory("id-1", "Content")]);

            Assert.That(response.Count, Is.EqualTo(response.Memories.Count));
        }

        // ── Ordering ────────────────────────────────────────────────────────────

        [Test]
        public void GivenMemoriesWithDifferentUpdatedDateTimes_WhenConstructing_ThenOrderedByUpdatedDateTimeDescending()
        {
            GptMemory earlierUpdated = BuildTestMemory("id-1", "Content 1");
            earlierUpdated.UpdatedDateTime = DateTimeOffset.Parse("2012-09-05T08:00:00.0000000+00:00");

            GptMemory laterUpdated = BuildTestMemory("id-2", "Content 2");
            laterUpdated.UpdatedDateTime = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");

            GetMemoriesResponse response = new([earlierUpdated, laterUpdated]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("id-2"));
            Assert.That(response.Memories[1].Id, Is.EqualTo("id-1"));
        }

        [Test]
        public void GivenMemoriesWithNullUpdatedDateTimes_WhenConstructing_ThenOrderedByCreatedDateTimeDescending()
        {
            GptMemory earlier = BuildTestMemory("id-1", "Content 1");
            earlier.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T08:00:00.0000000+00:00");
            earlier.UpdatedDateTime = null;

            GptMemory later = BuildTestMemory("id-2", "Content 2");
            later.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");
            later.UpdatedDateTime = null;

            GetMemoriesResponse response = new([earlier, later]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("id-2"));
            Assert.That(response.Memories[1].Id, Is.EqualTo("id-1"));
        }

        [Test]
        public void GivenMixedMemories_WhenConstructing_ThenUpdatedDateTimeTakesPriorityInOrdering()
        {
            GptMemory updatedMemory = BuildTestMemory("updated-id", "Updated content");
            updatedMemory.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T06:00:00.0000000+00:00");
            updatedMemory.UpdatedDateTime = DateTimeOffset.Parse("2012-09-05T12:00:00.0000000+00:00");

            GptMemory neverUpdatedMemory = BuildTestMemory("never-updated-id", "Never updated");
            neverUpdatedMemory.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T11:00:00.0000000+00:00");
            neverUpdatedMemory.UpdatedDateTime = null;

            GetMemoriesResponse response = new([neverUpdatedMemory, updatedMemory]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("updated-id"));
            Assert.That(response.Memories[1].Id, Is.EqualTo("never-updated-id"));
        }

        [Test]
        public void GivenMemoriesWithSameUpdatedDateTime_WhenConstructing_ThenTiesBrokenByCreatedDateTimeDescending()
        {
            DateTimeOffset sharedUpdatedAt = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");

            GptMemory earlierCreated = BuildTestMemory("id-1", "Content 1");
            earlierCreated.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T07:00:00.0000000+00:00");
            earlierCreated.UpdatedDateTime = sharedUpdatedAt;

            GptMemory laterCreated = BuildTestMemory("id-2", "Content 2");
            laterCreated.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T09:00:00.0000000+00:00");
            laterCreated.UpdatedDateTime = sharedUpdatedAt;

            GetMemoriesResponse response = new([earlierCreated, laterCreated]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("id-2"));
            Assert.That(response.Memories[1].Id, Is.EqualTo("id-1"));
        }

        [Test]
        public void GivenMemoriesWithSameNullUpdatedDateTimeAndSameCreatedDateTime_WhenConstructing_ThenAllMemoriesArePresent()
        {
            DateTimeOffset sharedCreatedAt = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");

            GptMemory firstMemory = BuildTestMemory("id-1", "Content 1");
            firstMemory.CreatedDateTime = sharedCreatedAt;

            GptMemory secondMemory = BuildTestMemory("id-2", "Content 2");
            secondMemory.CreatedDateTime = sharedCreatedAt;

            GetMemoriesResponse response = new([firstMemory, secondMemory]);

            Assert.That(response.Memories, Has.Count.EqualTo(2));
        }

        [Test]
        public void GivenMemoriesWithAllNullUpdatedDateTimes_WhenConstructing_ThenMostRecentCreatedDateTimeIsFirst()
        {
            GptMemory oldest = BuildTestMemory("id-oldest", "Oldest content");
            oldest.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T06:00:00.0000000+00:00");
            oldest.UpdatedDateTime = null;

            GptMemory middle = BuildTestMemory("id-middle", "Middle content");
            middle.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T08:00:00.0000000+00:00");
            middle.UpdatedDateTime = null;

            GptMemory newest = BuildTestMemory("id-newest", "Newest content");
            newest.CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");
            newest.UpdatedDateTime = null;

            GetMemoriesResponse response = new([oldest, middle, newest]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("id-newest"));
            Assert.That(response.Memories[1].Id, Is.EqualTo("id-middle"));
            Assert.That(response.Memories[2].Id, Is.EqualTo("id-oldest"));
        }

        [Test]
        public void GivenMemoriesWithAllNonNullUpdatedDateTimes_WhenConstructing_ThenMostRecentlyUpdatedIsFirst()
        {
            GptMemory firstUpdated = BuildTestMemory("id-first", "First content");
            firstUpdated.UpdatedDateTime = DateTimeOffset.Parse("2012-09-05T06:00:00.0000000+00:00");

            GptMemory lastUpdated = BuildTestMemory("id-last", "Last content");
            lastUpdated.UpdatedDateTime = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00");

            GetMemoriesResponse response = new([firstUpdated, lastUpdated]);

            Assert.That(response.Memories[0].Id, Is.EqualTo("id-last"));
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        static GptMemory BuildTestMemory(string id, string content) => new()
        {
            Id = id,
            CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:00:00.0000000+00:00"),
            UpdatedDateTime = null,
            Content = content,
            Source = "nucilandia.ro",
            Confidence = 0.95m
        };
    }
}
