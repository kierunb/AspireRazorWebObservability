using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Hybrid;
using Polly.CircuitBreaker;
using RazorWebApp.ViewComponents;
using ZiggyCreatures.Caching.Fusion;

namespace RazorWebApp.Services;

public class BlobService
{
    private readonly ILogger<BlobViewComponent> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IFusionCache _cache;

    // The segment size for buffering the response body in bytes.
    // The default is set to 80 KB(81920 Bytes) to avoid allocations on the LOH.
    private readonly int _chunkSize = 81920;

    public BlobService(
        ILogger<BlobViewComponent> logger,
        BlobServiceClient blobServiceClient,
        IFusionCache cache
    )
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _cache = cache;
    }

    public async Task<IEnumerable<string>> GetBlobChunksAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        //return await GetBlobChunksCached(containerName, blobName, cancellationToken);
        return await ReadChunksFromStreamAsync(containerName, blobName);
    }

    public async Task<string> GetBlobAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string content = await reader.ReadToEndAsync();
        return content;
    }

    /// <summary>
    /// Streams blob content as an async enumerable of string chunks for maximum memory efficiency
    /// This method is ideal for very large files as it doesn't load all chunks into memory at once
    /// </summary>
    public async IAsyncEnumerable<string> GetBlobChunksStreamAsync(
        string containerName, 
        string blobName, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        var buffer = new byte[_chunkSize];
        int bytesRead;
        int chunkIndex = 0;
        long totalBytesRead = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, _chunkSize, cancellationToken)) > 0)
        {
            totalBytesRead += bytesRead;
            var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            _logger.LogDebug(
                "Streaming chunk {ChunkIndex} ({BytesRead} bytes) from blob {BlobName}",
                chunkIndex++, bytesRead, blobName);
                
            yield return chunk;
        }

        _logger.LogInformation(
            "Completed streaming {TotalBytesRead} bytes in {TotalChunks} chunks from blob {BlobName} in container {ContainerName}",
            totalBytesRead, chunkIndex, blobName, containerName);
    }

    /// <summary>
    /// Gets blob content with memory-efficient streaming, returning the complete content as a single string
    /// Uses StringBuilder with capacity estimation to minimize allocations
    /// </summary>
    public async Task<string> GetBlobContentOptimizedAsync(
        string containerName, 
        string blobName, 
        CancellationToken cancellationToken = default)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        // Get blob size for StringBuilder capacity optimization
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var blobSize = properties.Value.ContentLength;
        
        // Initialize StringBuilder with appropriate capacity to minimize allocations
        var contentBuilder = new StringBuilder((int)Math.Min(blobSize, int.MaxValue));

        await foreach (var chunk in GetBlobChunksStreamAsync(containerName, blobName, cancellationToken))
        {
            contentBuilder.Append(chunk);
        }

        return contentBuilder.ToString();
    }

    // index starts from 0
    private string GetKey(string containerName, string blobName, int index = 0) =>
        $"{containerName}-{blobName}-{index}";

    private string GetTag(string containerName, string blobName) => $"{containerName}-{blobName}";

    private async Task<List<string>> ReadChunksFromStreamAsync(string containerName, string blobName, CancellationToken cancellationToken = default)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        List<string> chunks = [];
        var buffer = new byte[_chunkSize];
        int bytesRead;
        long totalBytesRead = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, _chunkSize, cancellationToken)) > 0)
        {
            chunks.Add(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            totalBytesRead += bytesRead;
        }

        _logger.LogInformation(
            "Read {TotalBytesRead} bytes in {Chunks} chunks from blob {BlobName} in container {ContainerName}",
            totalBytesRead,
            chunks.Count,
            blobName,
            containerName
        );

        return chunks;
    }

    private async Task<IEnumerable<string>> GetBlobChunksCached(
    string containerName,
    string blobName,
    CancellationToken cancellationToken = default
)
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        var blobProps = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var blobSize = blobProps.Value.ContentLength;
        var chunkCount = (int)Math.Ceiling((double)blobSize / _chunkSize);

        var firstChunk = await _cache.TryGetAsync<string>(GetKey(containerName, blobName), token: cancellationToken);
        if (firstChunk.HasValue)
        {
            List<string> chunks = [];
            chunks.Add(firstChunk.Value);
            for (int i = 1; i < chunkCount; i++)
            {
                var chunk = await _cache.TryGetAsync<string>(GetKey(containerName, blobName, i), token: cancellationToken);
                if (chunk.HasValue)
                {
                    chunks.Add(chunk.Value);
                }
                else
                {
                    _logger.LogWarning(
                        "Chunk {ChunkIndex} not found in cache for blob {BlobName} in container {ContainerName}",
                        i,
                        blobName,
                        containerName
                    );
                }
            }

            return chunks;
        }
        else
        {
            // If the first chunk is not in the cache, reload all chunks
            await _cache.RemoveByTagAsync(GetTag(containerName, blobName), token: cancellationToken);
            return await ReloadChunksCache(containerName, blobName, cancellationToken);
        }

    }

    private async Task<IEnumerable<string>> ReloadChunksCache(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default
    )
    {
        var blobContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        TimeSpan chacheDuration = TimeSpan.FromSeconds(30);
        List<string> chunks = [];

        using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

        var buffer = new byte[_chunkSize];
        int bytesRead;
        int chunkCount = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, _chunkSize)) > 0)
        {
            string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            chunks.Add(chunk);
            _cache.Set(
                key: GetKey(containerName, blobName, chunkCount++),
                value: chunk,
                options: new FusionCacheEntryOptions
                {
                    Duration = chacheDuration
                },
                tags: [GetTag(containerName, blobName)],
                cancellationToken
            );     
        }
        return chunks;
    }
}
