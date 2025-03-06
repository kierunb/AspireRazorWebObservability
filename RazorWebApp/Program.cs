using Microsoft.Extensions.Azure;
using RazorWebApp;
using RazorWebApp.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Output caching
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(10)));
    options.AddPolicy("Expire20", builder => builder.Expire(TimeSpan.FromSeconds(20)));
    options.AddPolicy("Expire30", builder => builder.Expire(TimeSpan.FromSeconds(30)));
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
builder.UseOpenTelemetry(enableAzureMonitor: true, enableAspireDashboard: false);
builder.Services.AddSingleton<AppMetricsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseOutputCache();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.MapBlobEndpoints();

app.Run();
