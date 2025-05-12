using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using System.Drawing;
using System.Text;

namespace RazorWebApp.ViewComponents;

public class BlobViewComponent : ViewComponent
{
    private readonly ILogger<BlobViewComponent> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly HybridCache _cache;

    //The segment size for buffering the response body in bytes.The default is set to 80 KB(81920 Bytes) to avoid allocations on the LOH.
    private const int ChunkSize = 81920;

    public BlobViewComponent(
        ILogger<BlobViewComponent> logger,
        BlobServiceClient blobServiceClient, 
        HybridCache cache)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _cache = cache;
    }

    public async Task<IViewComponentResult> InvokeAsync(string containerName, string blobName)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        string key = $"{containerName}/{blobName}/1";

        var blobProps = await blobClient.GetPropertiesAsync();


        // TODO: Check if the blob is cached (separate entry for check), if not, read it from the stream and cache in chunks
        // - create separate cache key for each chunk and dedicated Service cache


        using var stream = await blobClient.OpenReadAsync();

        List<string> htmlChunks = await ReadHtmlChunksFromStream(stream);

        return View("Default", htmlChunks.ToArray());
    }

    private async Task<List<string>> ReadHtmlChunksFromStream(Stream stream)
    {
        List<string> htmlChunks = [];
        var buffer = new byte[ChunkSize];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, ChunkSize)) > 0)
        {
            htmlChunks.Add(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        return htmlChunks;
    }

    private async Task<List<string>> ReadHtmlChunksFromStreamCached(Stream stream, string containerName, string blobName)
    {
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(15)
        };

        List<string> htmlChunks = [];
        var buffer = new byte[ChunkSize];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, ChunkSize)) > 0)
        {
            htmlChunks.Add(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        return htmlChunks;
    }
}
