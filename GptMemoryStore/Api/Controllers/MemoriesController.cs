using System;

using Microsoft.AspNetCore.Mvc;

using NuciAPI.Controllers;

using GptMemoryStore.Api.Requests;
using GptMemoryStore.Api.Responses;
using GptMemoryStore.Configuration;
using GptMemoryStore.Service;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MemoriesController(
        IMemoryService service,
        SecuritySettings securitySettings) : NuciApiController
    {
        private static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        private NuciApiAuthorisation Authorisation
            => NuciApiAuthorisation.ApiKey(securitySettings.ApiKey);

        [HttpPost]
        public ActionResult Create([FromBody] CreateMemoryRequest request)
            => ProcessRequest(
                request,
                () => service.Create(new()
                {
                    Content = request.Content,
                    Source = request.Source,
                    Confidence = request.Confidence
                }),
                Authorisation);

        [HttpGet]
        public ActionResult Get()
            => ProcessRequest(
                new GetMemoriesRequest(),
                () => new GetMemoriesResponse(service.Get()),
                Authorisation);

        [HttpGet("{id}")]
        public ActionResult Get([FromRoute(Name = "id")] string id)
            => ProcessRequest(
                new GetMemoryRequest { Id = id },
                () => new GetMemoryResponse(service.Get(id)),
                Authorisation);

        [HttpPut]
        public ActionResult Update([FromBody] UpdateMemoryRequest request)
            => ProcessRequest(
                request,
                () => service.Update(new()
                {
                    Id = request.Id,
                    UpdatedDateTime = DateTimeOffset.Now,
                    Content = request.Content,
                    Source = request.Source,
                    Confidence = request.Confidence
                }),
                Authorisation);

        [HttpDelete("{id}")]
        public ActionResult Delete([FromRoute(Name = "id")] string id)
            => ProcessRequest(
                new DeleteMemoryRequest { Id = id },
                () => service.Delete(id),
                Authorisation);
    }
}
