using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;

namespace RazorWebApp.Pages;

public class BlobModel : PageModel
{
    private readonly ILogger<BlobModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HybridCache _cache;

    private static Random _random = new();

    public string HtmlContent { get; private set; } = string.Empty;

    public BlobModel(
        ILogger<BlobModel> logger,
        IHttpClientFactory httpClientFactory,
        HybridCache cache)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }


    public async Task OnGet()
    {
        var httpClient = _httpClientFactory.CreateClient("blobs");

        var entryOptions = new HybridCacheEntryOptions
        {
            LocalCacheExpiration = TimeSpan.FromSeconds(15)
        };

        string cacheKey = $"html-content-{_random.Next(1, 100)}";
        _logger.LogInformation("Fetching for cache key: {CacheKey}", cacheKey);

        HtmlContent = await _cache.GetOrCreateAsync(
            cacheKey,
            async _ => await httpClient.GetStringAsync("/stream-html/web/500KB.html"),
            options: entryOptions
        );

        //HtmlContent = await httpClient.GetStringAsync("/stream-html/web/html-part.html");

    }
}

