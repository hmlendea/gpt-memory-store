using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class CreateMemoryRequest : NuciApiRequest
    {
        public string Id { get; set; }

        public string Content { get; set; }

        public string Source { get; set; }

        public decimal Confidence { get; set; }
    }
}
