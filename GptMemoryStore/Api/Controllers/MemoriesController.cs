using Microsoft.AspNetCore.Mvc;
using NuciAPI.Controllers;
using GptMemoryStore.Configuration;
using GptMemoryStore.Service;
using GptMemoryStore.Api.Requests;
using GptMemoryStore.Service.Models;
using System;
using GptMemoryStore.Api.Responses;

namespace GptMemoryStore.Api.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class MemoriesController(
        IMemoryService service,
        SecuritySettings securitySettings) : NuciApiController
    {
        static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        readonly NuciApiAuthorisation authorisation = NuciApiAuthorisation.ApiKey(securitySettings.ApiKey);

        [HttpPost]
        public ActionResult Create([FromBody] CreateMemoryRequest request)
            => ProcessRequest(
                request,
                () => service.Create(new GptMemory()
                {
                    Content = request.Content,
                    Source = request.Source,
                    Confidence = request.Confidence
                }),
                authorisation);

        [HttpGet]
        public ActionResult Get()
            => ProcessRequest(
                new GetMemoriesRequest(),
                () => new GptMemoriesResponse(service.Get()),
                authorisation);

        [HttpGet("{id}")]
        public ActionResult Get([FromRoute(Name = "id")] string id)
             => ProcessRequest(
                new GetMemoryRequest { Id = id },
                () => new GptMemoryResponse(service.Get(id)),
                authorisation);

        [HttpPut]
        public ActionResult Update([FromBody] UpdateMemoryRequest request)
            => ProcessRequest(
                request,
                () => service.Update(new GptMemory()
                {
                    Id = request.Id,
                    UpdatedDateTime = DateTimeOffset.Now,
                    Content = request.Content,
                    Source = request.Source,
                    Confidence = request.Confidence
                }),
                authorisation);

        [HttpDelete("{id}")]
        public ActionResult Delete([FromRoute(Name = "id")] string id)
             => ProcessRequest(
                new DeleteMemoryRequest { Id = id },
                () => service.Delete(id),
                authorisation);
    }
}
