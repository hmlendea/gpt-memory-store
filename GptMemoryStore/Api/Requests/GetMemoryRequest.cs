using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class GetMemoryRequest : NuciApiRequest
    {
        public required string Id { get; set; }
    }
}
