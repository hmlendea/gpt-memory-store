using GptMemoryStore.Mcp.Models;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Mcp.Mapping
{
    internal static class MemoryToolMappingExtensions
    {
        internal static MemoryToolResult ToDataObject(this GptMemory memory) => new()
        {
            Id = memory.Id,
            CreatedDateTime = memory.CreatedDateTime,
            UpdatedDateTime = memory.UpdatedDateTime,
            Content = memory.Content,
            Source = memory.Source,
            Confidence = memory.Confidence
        };
    }
}
