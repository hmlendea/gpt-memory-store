using System.Collections.Generic;
using System.Linq;
using GptMemoryStore.Service.Models;
using NuciAPI.Responses;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GptMemoriesResponse(IEnumerable<GptMemory> memories) : NuciApiSuccessResponse
    {
        public List<GptMemory> Memories { get; set; } = [.. memories
            .OrderByDescending(m => m.UpdatedDateTime)
            .ThenByDescending(m => m.CreatedDateTime)];

        public int Count => Memories?.Count ?? 0;
    }
}
