using System.Collections.Generic;

namespace GptMemoryStore.Mcp.Models
{
    public sealed class MemoryCollectionToolResult
    {
        public IEnumerable<MemoryToolResult> Memories { get; set; } = [];

        public int Count { get; set; }
    }
}
