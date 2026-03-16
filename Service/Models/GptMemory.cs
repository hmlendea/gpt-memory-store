using System;

namespace GptMemoryStore.Service.Models
{
    public class GptMemory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset? UpdatedDateTime { get; set; } = null;

        public string Content { get; set; }

        public string Source { get; set; }

        public decimal Confidence { get; set; }
    }
}
