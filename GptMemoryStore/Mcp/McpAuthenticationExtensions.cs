using System;
using System.Linq;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ModelContextProtocol.AspNetCore.Authentication;

using GptMemoryStore.Configuration;

namespace GptMemoryStore.Mcp
{
    public static class McpAuthenticationExtensions
    {
        public static string PolicyName => "McpAccess";

        public static string BearerScheme => "McpBearer";

        public static IServiceCollection AddMcpAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            McpSettings settings = new();
            configuration.GetSection(nameof(McpSettings)).Bind(settings);
            services.AddSingleton(settings);
            services.AddAuthentication();

            if (settings.IsEnabled)
            {
                ValidateSettings(settings);
                ConfigureAuthentication(services, settings);
            }

            return services;
        }

        private static void ValidateSettings(McpSettings settings)
        {
            Uri authority = new(settings.Authority, UriKind.Absolute);
            Uri resource = new(settings.Resource, UriKind.Absolute);

            if (!string.Equals(authority.Scheme, Uri.UriSchemeHttps) ||
                !string.Equals(resource.Scheme, Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("MCP authority and resource must use HTTPS.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(settings.RequiredScope);
        }

        private static void ConfigureAuthentication(
            IServiceCollection services,
            McpSettings settings)
        {
            services.AddAuthentication()
                .AddJwtBearer(BearerScheme, options => ConfigureBearer(options, settings))
                .AddMcp(options =>
                {
                    options.ForwardAuthenticate = BearerScheme;
                    options.ForwardForbid = BearerScheme;
                    options.ResourceMetadata = new()
                    {
                        Resource = new(settings.Resource),
                        AuthorizationServers = { new(settings.Authority) },
                        ScopesSupported = [settings.RequiredScope]
                    };
                });

            services.AddAuthorization(options => options.AddPolicy(PolicyName, policy => policy
                .AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.FindAll("scope")
                    .Concat(context.User.FindAll("scp"))
                    .Any(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Contains(settings.RequiredScope, StringComparer.Ordinal)))));
        }

        private static void ConfigureBearer(JwtBearerOptions options, McpSettings settings)
        {
            options.Authority = settings.Authority;
            options.Audience = settings.Resource;
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuer = settings.Authority;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            options.TokenValidationParameters.RequireSignedTokens = true;
            options.TokenValidationParameters.RequireExpirationTime = true;
            options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        }
    }
}
