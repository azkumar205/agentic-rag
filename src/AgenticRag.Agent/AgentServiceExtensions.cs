using AgenticRag.Agent.Configuration;
using AgenticRag.Agent.Interfaces;
using AgenticRag.Agent.Services;
using AgenticRag.Agent.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticRag.Agent;

/// <summary>
/// Extension methods for registering Agent services with the DI container.
/// </summary>
public static class AgentServiceExtensions
{
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName));

        services.AddMemoryCache();
        services.AddHttpClient("BingSearch", client =>
        {
            var apiKey = configuration["BingSearch:ApiKey"] ?? string.Empty;
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
        });

        services.AddSingleton<CacheService>();
        services.AddSingleton<IMemoryService, MemoryService>();
        services.AddSingleton<IChatThreadService, ChatThreadService>();

        // Register tools
        services.AddSingleton<IAgentTool, DocumentSearchTool>();
        services.AddSingleton<IAgentTool, SqlQueryTool>();
        services.AddSingleton<IAgentTool, WebSearchTool>();

        services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
