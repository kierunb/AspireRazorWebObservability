using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Azure;
using RazorWebApp;
using RazorWebApp.Endpoints;
using RazorWebApp.Services;
using Ganss.Xss;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Response Compression for better performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "text/html" });
});

// Rate Limiting for security (simplified for compatibility)
// In production, consider using more sophisticated rate limiting
// For now, we'll rely on other security measures

// Output caching
builder.Services.AddOutputCache(options =>
{
    // base policy is applied 'automatically' for evwerything (only for GET)

    // Create policies when calling AddOutputCache to specify caching configuration that applies to multiple endpoints.
    // A policy can be selected for specific endpoints,
    // while a "base policy" provides default caching configuration for a collection of endpoints.

    // for other policies use [OutputCache(Policy="name")] above PageModel
    //options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(10)));
    options.AddPolicy("Expire20", builder => builder.Expire(TimeSpan.FromSeconds(20)));
    options.AddPolicy("Expire30", builder => builder.Expire(TimeSpan.FromSeconds(30)));
});

builder.Services.AddHybridCache();

builder.Services.AddFusionCache();

builder.Services.AddTransient<BlobService>();

// HTML Sanitizer for security
builder.Services.AddScoped<IHtmlSanitizer>(provider =>
{
    var sanitizer = new HtmlSanitizer();
    
    // Configure allowed tags for HTML content
    sanitizer.AllowedTags.Clear();
    sanitizer.AllowedTags.UnionWith(new[] {
        "p", "div", "span", "h1", "h2", "h3", "h4", "h5", "h6",
        "strong", "em", "u", "b", "i", "br", "ul", "ol", "li", 
        "a", "img", "table", "thead", "tbody", "tr", "th", "td",
        "blockquote", "code", "pre"
    });
    
    // Configure allowed attributes
    sanitizer.AllowedAttributes.Clear();
    sanitizer.AllowedAttributes.Add("class");
    sanitizer.AllowedAttributes.Add("id");
    sanitizer.AllowedAttributes.Add("href");
    sanitizer.AllowedAttributes.Add("src");
    sanitizer.AllowedAttributes.Add("alt");
    sanitizer.AllowedAttributes.Add("title");
    
    // Only allow safe URL schemes
    sanitizer.AllowedSchemes.Clear();
    sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
    
    // Remove dangerous attributes
    sanitizer.RemovingAttribute += (sender, args) =>
    {
        if (args.Attribute.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            args.Cancel = false; // Allow removal of event handlers
        }
    };
    
    return sanitizer;
});

// HttpClient with resilience
builder.Services.AddHttpClient(
    "blobs",
    client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["AppBaseUrl"]!);
    }
).AddStandardResilienceHandler();

// Azure Storage Client
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetConnectionString("StorageAccount"));
});

// OpenTelemetry
builder.UseOpenTelemetry(
    enableAzureMonitor: false, 
    enableAspireDashboard: true, 
    enablePrometheus: false);

builder.Services.AddSingleton<AppMetricsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseResponseCompression();

app.UseRouting();

app.UseOutputCache();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();


app.MapBlobEndpoints();

app.Run();
