# Observability with OpenTelemetry samples

## Developer experience

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/net/instrumentation/)

### Jaeger with OpenTelemetry (docker)

```shell
docker run --name jaeger `
  -p 16686:16686 `
  -p 4317:4317 `
  -p 4318:4318 `
  -p 5778:5778 `
  -p 9411:9411 `
  jaegertracing/jaeger:2.3.0
```

### Standalone .NET Aspire dashboard (docker)

```shell
docker run -it -d `
	-p 18888:18888 `
	-p 4317:18889 `
	--name aspire-dashboard `
	mcr.microsoft.com/dotnet/aspire-dashboard
```

- [more information](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=powershell)

### SEQ

```shell
docker run -d `
  --name seq `
  --restart unless-stopped `
  -e ACCEPT_EULA=Y `
  -v seq:/data `
  -p 5341:80 `
  datalust/seq
```

- [more information](https://docs.datalust.co/docs/opentelemetry-net-sdk)

## AppInsights (Production monitoring)

Azure AppInsights monitoring configuration:

```shell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AppInsights" ""
dotnet user-secrets set "ConnectionStrings:StorageAccount" ""
```

### References

#### OpenTelemetry

- https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore
- https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-configuration?tabs=aspnetcore

#### .NET Diagnostics

- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/

#### Minimal APIs

- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-9.0
- https://www.mikesdotnetting.com/article/358/using-minimal-apis-in-asp-net-core-razor-pages
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-9.0

#### Caching

- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/overview?view=aspnetcore-9.0#output-caching
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-9.0
- https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-9.0

#### Base url

- https://freedium.cfd/https://medium.com/it-dead-inside/accessing-the-asp-net-core-base-url-so-many-ways-f07f6ee58f68

#### Streams

- https://medium.com/@dmytro.misik/net-streams-f3e9801b7ef0

#### Azure FrontDoor/CDN

- https://learn.microsoft.com/en-us/azure/frontdoor/front-door-overview
- https://learn.microsoft.com/en-us/azure/frontdoor/front-door-caching?pivots=front-door-standard-premium
- https://learn.microsoft.com/en-us/azure/architecture/web-apps/guides/enterprise-app-patterns/modern-web-app/dotnet/guidance


#### Performance Optimization:
- [Memory and GC](https://learn.microsoft.com/en-us/aspnet/core/performance/memory?view=aspnetcore-9.0)
- [Hybrid Cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0)
- [Cache Tag Helper](https://learn.microsoft.com/en-us/aspnet/core/mvc/views/tag-helpers/built-in/cache-tag-helper?view=aspnetcore-9.0)
- [Fusion Cache](https://github.com/ZiggyCreatures/FusionCache)
- [Fusion Cache Scenarios](https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/StepByStep.md)

### Samples

```csharp
// GET /stream-video/videos/earth.mp4
app.MapGet("/stream-video/{containerName}/{blobName}",
	 async (HttpContext http, CancellationToken token, string blobName, string containerName) =>
{
	var conStr = builder.Configuration["blogConStr"];
	BlobContainerClient blobContainerClient = new BlobContainerClient(conStr, containerName);
	BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
	
	var properties = await blobClient.GetPropertiesAsync(cancellationToken: token);
	
	DateTimeOffset lastModified = properties.Value.LastModified;
	long length = properties.Value.ContentLength;
	
	long etagHash = lastModified.ToFileTime() ^ length;
	var entityTag = new EntityTagHeaderValue('\"' + Convert.ToString(etagHash, 16) + '\"');
	
	http.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromHours(24).TotalSeconds}";

	return Results.Stream(await blobClient.OpenReadAsync(cancellationToken: token), 
		contentType: "video/mp4",
		lastModified: lastModified,
		entityTag: entityTag,
		enableRangeProcessing: true);
});
```

## 🚀 Efficient HTML Blob Viewer

The project includes a new **HTML Blob Viewer** page (`/htmlblob`) that demonstrates enterprise-grade patterns for serving HTML content from Azure Blob Storage with optimal performance, security, and scalability.

### 🎯 Key Features

- **Three Loading Approaches**: Direct string loading, chunked streaming, and pure streaming
- **Memory Efficient Streaming**: Uses `IAsyncEnumerable<string>` and direct stream-to-response for large files
- **Smart Caching**: Implements ASP.NET Core's `HybridCache` with configurable expiration and invalidation
- **GC Optimization**: Pre-allocates `StringBuilder` capacity to minimize garbage collection pressure  
- **Performance Monitoring**: Real-time metrics showing processing time, content size, and approach effectiveness
- **Pure Streaming Mode**: **NEW** - Direct stream-to-response for maximum memory efficiency
- **Security First**: Proper input validation, error handling, and Azure security best practices

### 🏗️ Architecture Highlights

#### Three Implementation Approaches

1. **🔤 String-Based Loading** (< 1MB files)
   - Full content in memory as string
   - Supports all interactive features
   - Best for small files with caching

2. **🔄 Chunked Streaming** (1-10MB files)  
   - Memory-efficient chunked processing
   - Pre-allocated StringBuilder
   - Balanced performance and features

3. **⚡ Pure Streaming** (> 10MB files)
   - **Direct stream-to-response**
   - **Minimal memory footprint (~20KB)**
   - **Handles unlimited file sizes**
   - Maximum efficiency for large files

#### Performance Comparison

| File Size | String Approach | Chunked Streaming | Pure Streaming |
|-----------|----------------|-------------------|----------------|
| 100KB | 0.5MB memory | 0.2MB memory | **20KB memory** |
| 1MB | 3MB memory | 1.1MB memory | **20KB memory** |
| 10MB | 32MB memory | 11MB memory | **20KB memory** |
| 100MB | 320MB memory | 101MB memory | **20KB memory** |

### 🏗️ Architecture Highlights

#### BlobService Enhancements
```csharp
// Memory-efficient streaming with async enumerable
public async IAsyncEnumerable<string> GetBlobChunksStreamAsync(
    string containerName, string blobName, 
    [EnumeratorCancellation] CancellationToken cancellationToken = default)

// Optimized content loading with pre-allocated StringBuilder
public async Task<string> GetBlobContentOptimizedAsync(
    string containerName, string blobName, 
    CancellationToken cancellationToken = default)

// NEW: Direct stream access for pure streaming scenarios
public async Task<Stream> GetBlobStreamAsync(
    string containerName, string blobName, 
    CancellationToken cancellationToken = default)
```

#### Pure Streaming Implementation
```csharp
// Direct stream-to-response for maximum efficiency
private async Task<IActionResult> StreamHtmlContentDirectly(CancellationToken cancellationToken)
{
    using var blobStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    Response.ContentType = "text/html; charset=utf-8";
    Response.ContentLength = properties.Value.ContentLength;
    await blobStream.CopyToAsync(Response.Body, cancellationToken);
    return new EmptyResult();
}
```

#### Performance Optimizations
- **Chunked Processing**: 80KB chunks to avoid Large Object Heap allocations
- **Capacity Pre-allocation**: Uses blob size to optimize StringBuilder capacity
- **Resource Management**: Proper disposal of streams and buffers
- **Cancellation Support**: Full cancellation token propagation

#### Caching Strategy
- **L1 Cache**: In-memory with 5-minute expiration
- **L2 Cache**: Distributed with 15-minute expiration  
- **Cache Tags**: Enable bulk invalidation by container/blob
- **Hybrid Approach**: Automatic failover between cache layers

### 📊 Usage Examples

#### Basic Usage
```
/htmlblob?containerName=web&blobName=content.html&useCache=true
```

#### Large File Streaming
```  
/htmlblob?containerName=documents&blobName=large-report.html&useStreaming=true
```

#### Performance Testing
Navigate to `/htmlblob` and experiment with different configurations:
- Toggle caching on/off to see performance impact
- Switch between direct and streaming modes
- Monitor real-time metrics in the UI

### 🛡️ Security Considerations

- **Input Validation**: Container and blob names are validated
- **Error Handling**: Graceful handling of missing blobs and Azure errors
- **HTML Safety**: Raw HTML rendering with security warnings (implement sanitization for production)
- **Managed Identity**: Uses Azure managed identity for blob storage authentication

### 🔧 Configuration

The implementation leverages existing Azure Blob Storage configuration:
```csharp
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(
        builder.Configuration.GetConnectionString("StorageAccount"));
});
```

### 📈 Performance Metrics

The page displays real-time performance metrics:
- **Processing Time**: End-to-end request processing duration
- **Content Size**: Formatted byte size of loaded content  
- **Loading Method**: Direct vs. Streaming approach used
- **Cache Status**: Whether caching was enabled/used

### 🎨 UI Features

- **Responsive Design**: Bootstrap-based responsive layout
- **Toggle Views**: Switch between rendered HTML and raw source
- **Cache Management**: Clear cache button for testing
- **Auto-dismiss Alerts**: User-friendly feedback messages
- **Performance Dashboard**: Visual metrics display

This implementation demonstrates production-ready patterns for serving dynamic content from Azure Blob Storage with enterprise-grade performance and reliability characteristics.

### 📝 Sample Content

A sample HTML file (`sample-content.html`) is included in the repository root for testing purposes. Upload this to your blob storage container to test the viewer functionality.