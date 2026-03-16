using System.Collections.Generic;
using GptMemoryStore.Service.Models;
using NuciAPI.Responses;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GptMemoriesResponse(IEnumerable<GptMemory> memories) : NuciApiSuccessResponse
    {
        public List<GptMemory> Memories { get; set; } = [.. memories];

        public int Count => Memories?.Count ?? 0;
    }
}
