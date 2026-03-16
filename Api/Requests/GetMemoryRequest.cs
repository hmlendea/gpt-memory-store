using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class GetMemoryRequest : NuciApiRequest
    {
        public string Id { get; set; }
    }
}
