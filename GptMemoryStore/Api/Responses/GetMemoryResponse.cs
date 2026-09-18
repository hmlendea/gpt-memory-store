using System;
using System.Text.Json.Serialization;

using NuciAPI.Responses;
using NuciSecurity.HMAC;

using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Api.Responses
{
    public sealed class GetMemoryResponse(GptMemory memory) : NuciApiResponseContent
    {
        [HmacOrder(1)]
        [JsonPropertyName("id")]
        public string Identifier { get; set; } = memory.Id;

        [HmacOrder(2)]
        public DateTimeOffset CreatedDateTime { get; set; } = memory.CreatedDateTime;

        [HmacOrder(3)]
        public DateTimeOffset? UpdatedDateTime { get; set; } = memory.UpdatedDateTime;

        [HmacOrder(4)]
        public string Content { get; set; } = memory.Content;

        [HmacOrder(5)]
        public string Source { get; set; } = memory.Source;

        [HmacOrder(6)]
        public decimal Confidence { get; set; } = memory.Confidence;
    }
}
