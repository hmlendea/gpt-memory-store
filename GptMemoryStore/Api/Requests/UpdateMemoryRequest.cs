using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class UpdateMemoryRequest : NuciApiRequest
    {
        public required string Id { get; set; }

        public required string Content { get; set; }

        public required string Source { get; set; }

        public required decimal Confidence { get; set; }
    }
}
