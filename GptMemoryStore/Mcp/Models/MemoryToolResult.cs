using System;

namespace GptMemoryStore.Mcp.Models
{
    public sealed class MemoryToolResult
    {
        public string Id { get; set; } = string.Empty;

        public DateTimeOffset CreatedDateTime { get; set; }

        public DateTimeOffset? UpdatedDateTime { get; set; }

        public string Content { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public decimal Confidence { get; set; }
    }
}
