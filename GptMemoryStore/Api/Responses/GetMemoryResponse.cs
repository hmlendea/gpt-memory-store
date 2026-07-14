using NuciAPI.Responses;

using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GetMemoryResponse(GptMemory memory) : NuciApiSuccessResponse
    {
        private static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        public string Id { get; set; } = memory.Id;

        public string CreatedDateTime { get; set; } = memory.CreatedDateTime.ToString(DateTimeFormat);

        public string UpdatedDateTime { get; set; } = memory.UpdatedDateTime?.ToString(DateTimeFormat);

        public string Content { get; set; } = memory.Content;

        public string Source { get; set; } = memory.Source;

        public decimal Confidence { get; set; } = memory.Confidence;
    }
}
