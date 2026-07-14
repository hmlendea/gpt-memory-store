using System.Collections.Generic;
using System.Linq;

using NuciAPI.Responses;

using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GetMemoriesResponse(IEnumerable<GptMemory> memories) : NuciApiSuccessResponse
    {
        public List<GptMemory> Memories { get; set; } = [.. memories
            .OrderByDescending(memory => memory.UpdatedDateTime)
            .ThenByDescending(memory => memory.CreatedDateTime)];

        public int Count => Memories.Count;
    }
}
