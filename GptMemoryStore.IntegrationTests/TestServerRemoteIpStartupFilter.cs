using System;
using System.Net;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace GptMemoryStore.IntegrationTests
{
    public sealed class TestServerRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => application =>
            {
                application.Use(nextMiddleware => async context =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Loopback;
                    await nextMiddleware(context);
                });

                next(application);
            };
    }
}