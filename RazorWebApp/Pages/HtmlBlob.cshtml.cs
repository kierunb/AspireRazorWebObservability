using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Hybrid;
using RazorWebApp.Services;
using System.Text;
using System.Diagnostics;
using Azure.Storage.Blobs;
using Ganss.Xss;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.RateLimiting;

namespace RazorWebApp.Pages;

/// <summary>
/// Efficient HTML blob viewer page with streaming, caching, and memory optimization
/// </summary>
[OutputCache(PolicyName = "Expire30")]
public class HtmlBlobModel : PageModel, IDisposable
{
    private readonly ILogger<HtmlBlobModel> _logger;
    private readonly BlobService _blobService;
    private readonly HybridCache _cache;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IHtmlSanitizer _htmlSanitizer;
    
    // Input validation patterns
    private static readonly Regex ContainerNamePattern = new(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);
    private static readonly Regex BlobNamePattern = new(@"^[a-zA-Z0-9][a-zA-Z0-9\-_.]*\.(html|htm)$", RegexOptions.Compiled);
    
    // Properties for the page
    public string? HtmlContent { get; private set; }
    public Stream? HtmlContentStream { get; private set; }
    public string? ErrorMessage { get; private set; }
    public long ContentSize { get; private set; }
    public TimeSpan ProcessingTime { get; private set; }
    public bool IsStreaming { get; private set; }
    public bool IsStreamingMode { get; private set; }
    
    // Query parameters
    [BindProperty(SupportsGet = true)]
    public string ContainerName { get; set; } = "web";
    
    [BindProperty(SupportsGet = true)]
    public string BlobName { get; set; } = "500KB.html";
    
    [BindProperty(SupportsGet = true)]
    public bool UseCache { get; set; } = true;
    
    [BindProperty(SupportsGet = true)]
    public bool UseStreaming { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public bool UsePureStreaming { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public bool UseServerSideStreaming { get; set; } = false;

    [BindProperty(SupportsGet = true)]
    public bool UseViewStreaming { get; set; } = false;

    public HtmlBlobModel(
        ILogger<HtmlBlobModel> logger,
        BlobService blobService,
        HybridCache cache,
        BlobServiceClient blobServiceClient,
        IHtmlSanitizer htmlSanitizer)
    {
        _logger = logger;
        _blobService = blobService;
        _cache = cache;
        _blobServiceClient = blobServiceClient;
        _htmlSanitizer = htmlSanitizer;
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
            if (!ValidateInputs())
            {
                return Page();
            }

            // Log the request
            _logger.LogInformation(
                "Loading HTML blob {BlobName} from container {ContainerName}. UseCache: {UseCache}, UseStreaming: {UseStreaming}, UsePureStreaming: {UsePureStreaming}, UseServerSideStreaming: {UseServerSideStreaming}, UseViewStreaming: {UseViewStreaming}",
                BlobName, ContainerName, UseCache, UseStreaming, UsePureStreaming, UseServerSideStreaming, UseViewStreaming);

            if (UsePureStreaming)
            {
                // Use pure streaming approach - stream directly to response
                return await StreamHtmlContentDirectly(cancellationToken);
            }
            else if (UseViewStreaming)
            {
                // Use view-level streaming - pass stream to the view for consumption
                await LoadHtmlContentStreamForView(cancellationToken);
                IsStreamingMode = true;
            }
            else if (UseServerSideStreaming)
            {
                // Use server-side streaming approach - process stream on server for SEO
                await LoadHtmlContentServerSideStreaming(cancellationToken);
                IsStreaming = true;
            }
            else if (UseStreaming)
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
            ErrorMessage = $"Content not found.";
            _logger.LogWarning("Blob not found: {ContainerName}/{BlobName}", ContainerName, BlobName);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Unable to load content. Please try again later.";
            _logger.LogError(ex, "Error loading HTML blob {ContainerName}/{BlobName}", ContainerName, BlobName);
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
    /// Streams HTML content directly to the response without loading into memory
    /// This is the most memory-efficient approach for very large files
    /// </summary>
    private async Task<IActionResult> StreamHtmlContentDirectly(CancellationToken cancellationToken)
    {
        try
        {
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = blobContainerClient.GetBlobClient(BlobName);
            
            // Get blob properties for content length and validation
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            ContentSize = properties.Value.ContentLength;
            
            // Set response headers for HTML content
            Response.ContentType = "text/html; charset=utf-8";
            Response.ContentLength = ContentSize;
            
            // Add cache headers if caching is enabled
            if (UseCache)
            {
                Response.Headers.CacheControl = "public, max-age=300"; // 5 minutes
                Response.Headers.ETag = properties.Value.ETag.ToString();
            }
            
            // Stream directly to response
            using var blobStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
            await blobStream.CopyToAsync(Response.Body, cancellationToken);
            
            _logger.LogInformation(
                "Successfully streamed {ContentSize} bytes directly to response for blob {BlobName}",
                ContentSize, BlobName);
                
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming HTML content directly for blob {BlobName}", BlobName);
            
            // Fall back to error page
            ErrorMessage = "An error occurred while streaming the HTML content.";
            return Page();
        }
    }

    /// <summary>
    /// Loads HTML content as a stream for partial streaming scenarios
    /// Allows for some processing while maintaining memory efficiency
    /// </summary>
    private async Task LoadHtmlContentAsStream(CancellationToken cancellationToken)
    {
        // Dispose any existing stream
        HtmlContentStream?.Dispose();
        
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = blobContainerClient.GetBlobClient(BlobName);
        
        // Get properties for content size
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        ContentSize = properties.Value.ContentLength;
        
        // Open stream (will be disposed by the page lifecycle)
        HtmlContentStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        IsStreamingMode = true;
        
        _logger.LogInformation(
            "Opened stream for blob {BlobName} with size {ContentSize} bytes",
            BlobName, ContentSize);
    }

    /// <summary>
    /// Loads HTML content using server-side streaming for SEO optimization
    /// Processes the stream on the server and renders content directly in the page
    /// This approach provides the best of both worlds: memory efficiency and SEO compatibility
    /// </summary>
    private async Task LoadHtmlContentServerSideStreaming(CancellationToken cancellationToken)
    {
        if (UseCache)
        {
            // For server-side streaming, we can still use cache for the final rendered content
            var cacheKey = $"html-blob-server-stream-{ContainerName}-{BlobName}";
            var cacheOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Expiration = TimeSpan.FromMinutes(15)
            };

            HtmlContent = await _cache.GetOrCreateAsync(
                cacheKey,
                async _ => await ProcessStreamOnServer(cancellationToken),
                options: cacheOptions,
                cancellationToken: cancellationToken
            );
        }
        else
        {
            HtmlContent = await ProcessStreamOnServer(cancellationToken);
        }
    }

    /// <summary>
    /// Processes the blob stream on the server for SEO-friendly rendering
    /// Uses async enumerable for memory efficiency while maintaining server-side processing
    /// </summary>
    private async Task<string> ProcessStreamOnServer(CancellationToken cancellationToken)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName: BlobName);
        
        // Get blob properties for optimization
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        ContentSize = properties.Value.ContentLength;
        
        // Use StringBuilder with pre-allocated capacity for optimal memory usage
        var contentBuilder = new StringBuilder(capacity: (int)Math.Min(ContentSize, int.MaxValue));
        
        _logger.LogInformation(
            "Starting server-side stream processing for blob {BlobName} ({ContentSize} bytes)",
            BlobName, ContentSize);
        
        // Process stream using async enumerable for memory efficiency
        await foreach (var chunk in _blobService.GetBlobChunksStreamAsync(ContainerName, BlobName, cancellationToken))
        {
            contentBuilder.Append(chunk);
        }
        
        var result = contentBuilder.ToString();
        
        _logger.LogInformation(
            "Completed server-side stream processing for blob {BlobName}. Final content length: {FinalLength} characters",
            BlobName, result.Length);
            
        return result;
    }

    /// <summary>
    /// Loads HTML content as a stream for view-level consumption
    /// Best for: SEO-friendly rendering with memory efficiency
    /// The view will consume the stream directly using C# code
    /// </summary>
    private async Task LoadHtmlContentStreamForView(CancellationToken cancellationToken)
    {
        try
        {
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = blobContainerClient.GetBlobClient(BlobName);
            
            // Get blob properties
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            ContentSize = properties.Value.ContentLength;
            
            // Get the stream and assign it to the property
            // The stream will be consumed in the view
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            HtmlContentStream = response.Value.Content;
            
            _logger.LogInformation(
                "Prepared stream for view consumption: blob {BlobName} ({ContentSize} bytes)",
                BlobName, ContentSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing stream for view: {BlobName}", BlobName);
            throw;
        }
    }

    /// <summary>
    /// Helper method for the view to safely consume the HTML stream
    /// This method ensures proper encoding and error handling
    /// </summary>
    public async Task<string> ReadStreamContentAsync()
    {
        if (HtmlContentStream == null)
        {
            return string.Empty;
        }

        try
        {
            // Reset stream position if needed
            if (HtmlContentStream.CanSeek && HtmlContentStream.Position != 0)
            {
                HtmlContentStream.Seek(0, SeekOrigin.Begin);
            }

            using var reader = new StreamReader(HtmlContentStream, Encoding.UTF8, leaveOpen: true);
            var content = await reader.ReadToEndAsync();
            
            _logger.LogInformation(
                "Successfully read {ContentLength} characters from stream in view",
                content.Length);
                
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading stream content in view");
            return $"<div class='alert alert-danger'>Error reading stream content: {ex.Message}</div>";
        }
    }

    /// <summary>
    /// Helper method for the view to consume the HTML stream in chunks
    /// This provides better memory efficiency for very large files
    /// </summary>
    public async IAsyncEnumerable<string> ReadStreamChunksAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (HtmlContentStream == null)
        {
            yield break;
        }

        StreamReader? reader = null;
        try
        {
            // Reset stream position if needed
            if (HtmlContentStream.CanSeek && HtmlContentStream.Position != 0)
            {
                HtmlContentStream.Seek(0, SeekOrigin.Begin);
            }

            reader = new StreamReader(HtmlContentStream, Encoding.UTF8, leaveOpen: true);
            
            var buffer = new char[8192]; // 8KB chunks
            int bytesRead;
            
            while ((bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new string(buffer, 0, bytesRead);
            }
            
            _logger.LogInformation("Completed chunked stream reading in view");
        }
        finally
        {
            reader?.Dispose();
        }
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

        return RedirectToPage(new { ContainerName, BlobName, UseCache, UseStreaming, UseServerSideStreaming, UsePureStreaming, UseViewStreaming });
    }

    /// <summary>
    /// Validates input parameters to prevent security issues
    /// </summary>
    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(ContainerName) || string.IsNullOrWhiteSpace(BlobName))
        {
            ErrorMessage = "Container name and blob name are required.";
            return false;
        }

        if (!ContainerNamePattern.IsMatch(ContainerName))
        {
            ErrorMessage = "Invalid container name format.";
            _logger.LogWarning("Invalid container name format: {ContainerName}", ContainerName);
            return false;
        }

        if (!BlobNamePattern.IsMatch(BlobName))
        {
            ErrorMessage = "Invalid blob name format. Only HTML files are allowed.";
            _logger.LogWarning("Invalid blob name format: {BlobName}", BlobName);
            return false;
        }

        if (ContainerName.Length > 63 || BlobName.Length > 1024)
        {
            ErrorMessage = "Container or blob name is too long.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sanitizes HTML content to prevent XSS attacks
    /// </summary>
    public string GetSanitizedContent()
    {
        if (string.IsNullOrEmpty(HtmlContent))
            return string.Empty;
            
        try
        {
            var sanitized = _htmlSanitizer.Sanitize(HtmlContent);
            
            _logger.LogDebug("HTML content sanitized. Original length: {Original}, Sanitized length: {Sanitized}",
                HtmlContent.Length, sanitized.Length);
                
            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing HTML content");
            return "<div class='alert alert-danger'>Content could not be displayed safely.</div>";
        }
    }

    /// <summary>
    /// Sanitizes stream content read in the view
    /// </summary>
    public async Task<string> GetSanitizedStreamContentAsync()
    {
        try
        {
            var content = await ReadStreamContentAsync();
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var sanitized = _htmlSanitizer.Sanitize(content);
            
            _logger.LogDebug("Stream content sanitized. Original length: {Original}, Sanitized length: {Sanitized}",
                content.Length, sanitized.Length);
                
            return sanitized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing stream content");
            return "<div class='alert alert-danger'>Stream content could not be displayed safely.</div>";
        }
    }

    private bool _disposed = false;

    /// <summary>
    /// Dispose resources properly
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                HtmlContentStream?.Dispose();
                HtmlContentStream = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing HTML content stream");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
