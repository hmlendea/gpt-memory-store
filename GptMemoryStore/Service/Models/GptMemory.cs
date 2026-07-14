using System;

namespace GptMemoryStore.Service.Models
{
    public sealed class GptMemory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset? UpdatedDateTime { get; set; }

        public string Content { get; set; }

        public string Source { get; set; }

        public decimal Confidence { get; set; }
    }
}
