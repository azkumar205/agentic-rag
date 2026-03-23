using AgenticRag.DataAccess.Configuration;
using AgenticRag.DataAccess.Interfaces;
using AgenticRag.DataAccess.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgenticRag.DataAccess;

/// <summary>
/// Extension methods for registering DataAccess services with the DI container.
/// </summary>
public static class DataAccessServiceExtensions
{
    public static IServiceCollection AddDataAccessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureStorageOptions>(configuration.GetSection(AzureStorageOptions.SectionName));
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));
        services.Configure<AzureOpenAiOptions>(configuration.GetSection(AzureOpenAiOptions.SectionName));
        services.Configure<DocumentIntelligenceOptions>(configuration.GetSection(DocumentIntelligenceOptions.SectionName));
        services.Configure<SqlOptions>(configuration.GetSection(SqlOptions.SectionName));

        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddSingleton<IEmbeddingService, EmbeddingService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IDocumentExtractionService, DocumentExtractionService>();
        services.AddSingleton<IDocumentIngestionService, DocumentIngestionService>();
        services.AddSingleton<ISqlDataService, SqlDataService>();

        return services;
    }
}
