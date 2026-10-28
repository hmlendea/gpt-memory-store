using NuciAPI.Requests;

namespace GptMemoryStore.Api.Requests
{
    public sealed class CreateMemoryRequest : NuciApiRequest
    {
        public string Id { get; set; } = string.Empty;

        public required string Content { get; set; }

        public required string Source { get; set; }

        public required decimal Confidence { get; set; }
    }
}
