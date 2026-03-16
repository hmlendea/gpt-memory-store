using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class DeleteMemoryRequest : NuciApiRequest
    {
        public string Id { get; set; }
    }
}
