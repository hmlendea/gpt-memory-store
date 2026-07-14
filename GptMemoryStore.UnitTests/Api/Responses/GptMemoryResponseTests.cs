using System;
using GptMemoryStore.Api.Responses;
using GptMemoryStore.Service.Models;
using NUnit.Framework;

namespace GptMemoryStore.UnitTests.Api.Responses
{
    [TestFixture]
    public sealed class GetMemoryResponseTests
    {
        static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        // ── Constructor ─────────────────────────────────────────────────────────

        [Test]
        public void GivenMemory_WhenConstructing_ThenIdIsSet()
        {
            GptMemory memory = BuildTestMemory();

            GetMemoryResponse response = new(memory);

            Assert.That(response.Id, Is.EqualTo(memory.Id));
        }

        [Test]
        public void GivenMemory_WhenConstructing_ThenCreatedDateTimeIsFormattedCorrectly()
        {
            GptMemory memory = BuildTestMemory();

            GetMemoryResponse response = new(memory);

            string expectedCreatedDateTime = memory.CreatedDateTime.ToString(DateTimeFormat);
            Assert.That(response.CreatedDateTime, Is.EqualTo(expectedCreatedDateTime));
        }

        [Test]
        public void GivenMemoryWithUpdatedDateTime_WhenConstructing_ThenUpdatedDateTimeIsFormattedCorrectly()
        {
            DateTimeOffset updatedAt = DateTimeOffset.Parse("2012-09-05T12:00:00.0000000+00:00");
            GptMemory memory = BuildTestMemory();
            memory.UpdatedDateTime = updatedAt;

            GetMemoryResponse response = new(memory);

            string expectedUpdatedDateTime = updatedAt.ToString(DateTimeFormat);
            Assert.That(response.UpdatedDateTime, Is.EqualTo(expectedUpdatedDateTime));
        }

        [Test]
        public void GivenMemoryWithNullUpdatedDateTime_WhenConstructing_ThenUpdatedDateTimeIsNull()
        {
            GptMemory memory = BuildTestMemory();
            memory.UpdatedDateTime = null;

            GetMemoryResponse response = new(memory);

            Assert.That(response.UpdatedDateTime, Is.Null);
        }

        [Test]
        public void GivenMemory_WhenConstructing_ThenContentIsSet()
        {
            GptMemory memory = BuildTestMemory();

            GetMemoryResponse response = new(memory);

            Assert.That(response.Content, Is.EqualTo(memory.Content));
        }

        [Test]
        public void GivenMemory_WhenConstructing_ThenSourceIsSet()
        {
            GptMemory memory = BuildTestMemory();

            GetMemoryResponse response = new(memory);

            Assert.That(response.Source, Is.EqualTo(memory.Source));
        }

        [Test]
        public void GivenMemory_WhenConstructing_ThenConfidenceIsSet()
        {
            GptMemory memory = BuildTestMemory();

            GetMemoryResponse response = new(memory);

            Assert.That(response.Confidence, Is.EqualTo(memory.Confidence));
        }

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        public void GivenMemoryWithGivenConfidence_WhenConstructing_ThenConfidenceMatchesInput(
            double confidenceAsDouble)
        {
            decimal confidence = (decimal)confidenceAsDouble;
            GptMemory memory = BuildTestMemory();
            memory.Confidence = confidence;

            GetMemoryResponse response = new(memory);

            Assert.That(response.Confidence, Is.EqualTo(confidence));
        }

        [Test]
        public void GivenMemoryWithEmptyContent_WhenConstructing_ThenContentIsEmpty()
        {
            GptMemory memory = BuildTestMemory();
            memory.Content = string.Empty;

            GetMemoryResponse response = new(memory);

            Assert.That(response.Content, Is.Empty);
        }

        [Test]
        public void GivenMemoryWithEmptySource_WhenConstructing_ThenSourceIsEmpty()
        {
            GptMemory memory = BuildTestMemory();
            memory.Source = string.Empty;

            GetMemoryResponse response = new(memory);

            Assert.That(response.Source, Is.Empty);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        static GptMemory BuildTestMemory() => new()
        {
            Id = "test-memory-id",
            CreatedDateTime = DateTimeOffset.Parse("2012-09-05T10:30:00.0000000+00:00"),
            UpdatedDateTime = null,
            Content = "Solaire of Astora likes the sun.",
            Source = "nucilandia.ro",
            Confidence = 0.95m
        };
    }
}
