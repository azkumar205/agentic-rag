using AgenticRag.Agent.Interfaces;
using AgenticRag.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgenticRag.Agent.Services;

/// <summary>
/// In-memory cache service for queries, retrieved context, and reasoning steps.
/// Reduces redundant LLM calls and improves response latency.
/// </summary>
public class CacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(60);

    public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool TryGetAgentResponse(string key, out AgentResponse? response)
    {
        var hit = _cache.TryGetValue(CacheKey(key), out response);
        if (hit) _logger.LogInformation("Cache HIT for key: {Key}", key);
        return hit;
    }

    public void SetAgentResponse(string key, AgentResponse response, TimeSpan? expiry = null)
    {
        _cache.Set(CacheKey(key), response, expiry ?? _defaultExpiry);
        _logger.LogInformation("Cached agent response for key: {Key}", key);
    }

    public bool TryGetSearchResult(string key, out SearchResult? result)
    {
        return _cache.TryGetValue(CacheKey(key), out result);
    }

    public void SetSearchResult(string key, SearchResult result, TimeSpan? expiry = null)
    {
        _cache.Set(CacheKey(key), result, expiry ?? _defaultExpiry);
    }

    private static string CacheKey(string input) => $"rag:{input.ToLowerInvariant().Trim()}";
}
