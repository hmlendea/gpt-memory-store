using System;
using System.IO;
using GptMemoryStore.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuciAPI.Middleware;

namespace GptMemoryStore
{
    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration => configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services
                .AddNuciApiReplayProtection()
                .AddConfigurations(Configuration)
                .AddCustomServices();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Ensure the stores exist
            var dataStoreSettings = app.ApplicationServices.GetRequiredService<DataStoreSettings>();
            CreateStoreIfMissing(dataStoreSettings.MemoryStorePath);

            app.UseNuciApiRequestLogging();
            app.UseNuciApiExceptionHandling();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        static void CreateStoreIfMissing(string storePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storePath);

            var storeDirectory = Path.GetDirectoryName(storePath);

            if (!Directory.Exists(storeDirectory))
            {
                Directory.CreateDirectory(storeDirectory);
            }

            if (!File.Exists(storePath))
            {
                File.WriteAllText(storePath, "[]");
            }
        }
    }
}
