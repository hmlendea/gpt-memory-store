using System.Collections.Generic;
using System.Linq;

using NuciAPI.Responses;
using NuciSecurity.HMAC;

using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GetMemoriesResponse : NuciApiResponseContent
    {
        [HmacOrder(1)]
        public IEnumerable<GetMemoryResponse> Memories { get; set; }

        [HmacIgnore]
        public int Count
        {
            get
            {
                if (Memories is null)
                {
                    return 0;
                }

                return Memories.Count();
            }
        }

        public GetMemoriesResponse()
        {
        }

        public GetMemoriesResponse(IEnumerable<GptMemory> memories)
        {
            Memories = memories
                .OrderByDescending(memory => memory.UpdatedDateTime)
                .ThenByDescending(memory => memory.CreatedDateTime)
                .Select(memory => new GetMemoryResponse(memory));
        }
    }
}
