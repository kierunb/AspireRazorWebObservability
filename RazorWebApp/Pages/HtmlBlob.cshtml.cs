using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;
using RazorWebApp.Services;
using System.Text;
using System.Diagnostics;

namespace RazorWebApp.Pages;

/// <summary>
/// Efficient HTML blob viewer page with streaming, caching, and memory optimization
/// </summary>
[OutputCache(PolicyName = "Expire30")]
public class HtmlBlobModel : PageModel
{
    private readonly ILogger<HtmlBlobModel> _logger;
    private readonly BlobService _blobService;
    private readonly HybridCache _cache;
    
    // Properties for the page
    public string? HtmlContent { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long ContentSize { get; private set; }
    public TimeSpan ProcessingTime { get; private set; }
    public bool IsStreaming { get; private set; }
    
    // Query parameters
    [BindProperty(SupportsGet = true)]
    public string ContainerName { get; set; } = "web";
    
    [BindProperty(SupportsGet = true)]
    public string BlobName { get; set; } = "500KB.html";
    
    [BindProperty(SupportsGet = true)]
    public bool UseCache { get; set; } = true;
    
    [BindProperty(SupportsGet = true)]
    public bool UseStreaming { get; set; } = false;

    public HtmlBlobModel(
        ILogger<HtmlBlobModel> logger,
        BlobService blobService,
        HybridCache cache)
    {
        _logger = logger;
        _blobService = blobService;
        _cache = cache;
    }

    /// <summary>
    /// Handles GET requests with optimized blob content loading
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Validate input parameters
            if (string.IsNullOrWhiteSpace(ContainerName) || string.IsNullOrWhiteSpace(BlobName))
            {
                ErrorMessage = "Container name and blob name are required.";
                return Page();
            }

            // Log the request
            _logger.LogInformation(
                "Loading HTML blob {BlobName} from container {ContainerName}. UseCache: {UseCache}, UseStreaming: {UseStreaming}",
                BlobName, ContainerName, UseCache, UseStreaming);

            if (UseStreaming)
            {
                // Use streaming approach for large files or when specified
                await LoadHtmlContentStreaming(cancellationToken);
                IsStreaming = true;
            }
            else
            {
                // Use direct load with caching for smaller files
                await LoadHtmlContentDirect(cancellationToken);
                IsStreaming = false;
            }

            ContentSize = Encoding.UTF8.GetByteCount(HtmlContent ?? string.Empty);
            
            _logger.LogInformation(
                "Successfully loaded HTML content. Size: {ContentSize} bytes, Processing time: {ProcessingTime}ms",
                ContentSize, stopwatch.ElapsedMilliseconds);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            ErrorMessage = $"Blob '{BlobName}' not found in container '{ContainerName}'.";
            _logger.LogWarning("Blob not found: {BlobName} in {ContainerName}", BlobName, ContainerName);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while loading the HTML content.";
            _logger.LogError(ex, "Error loading HTML blob {BlobName} from {ContainerName}", BlobName, ContainerName);
        }
        finally
        {
            ProcessingTime = stopwatch.Elapsed;
        }

        return Page();
    }

    /// <summary>
    /// Loads HTML content using direct approach with hybrid caching
    /// Best for smaller files (< 1MB) and frequently accessed content
    /// </summary>
    private async Task LoadHtmlContentDirect(CancellationToken cancellationToken)
    {
        if (UseCache)
        {
            var cacheKey = $"html-blob-{ContainerName}-{BlobName}";
            var cacheOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(15)
            };

            HtmlContent = await _cache.GetOrCreateAsync(
                cacheKey,
                async _ => await _blobService.GetBlobAsync(ContainerName, BlobName, cancellationToken),
                options: cacheOptions,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            HtmlContent = await _blobService.GetBlobAsync(ContainerName, BlobName, cancellationToken);
        }
    }

    /// <summary>
    /// Loads HTML content using streaming approach with chunked processing
    /// Best for larger files (> 1MB) to minimize memory allocation
    /// </summary>
    private async Task LoadHtmlContentStreaming(CancellationToken cancellationToken)
    {
        // Use the optimized streaming method that pre-allocates StringBuilder capacity
        HtmlContent = await _blobService.GetBlobContentOptimizedAsync(ContainerName, BlobName, cancellationToken);
    }

    /// <summary>
    /// Action method to clear cache for the current blob
    /// </summary>
    public async Task<IActionResult> OnPostClearCacheAsync()
    {
        try
        {
            var cacheKey = $"html-blob-{ContainerName}-{BlobName}";
            await _cache.RemoveAsync(cacheKey);
            
            _logger.LogInformation("Cache cleared for blob {BlobName} in {ContainerName}", BlobName, ContainerName);
            TempData["Message"] = "Cache cleared successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache for blob {BlobName} in {ContainerName}", BlobName, ContainerName);
            TempData["Error"] = "Error clearing cache.";
        }

        return RedirectToPage(new { ContainerName, BlobName, UseCache, UseStreaming });
    }
}
