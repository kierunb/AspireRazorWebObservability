using Microsoft.Extensions.Azure;
using RazorWebApp;
using RazorWebApp.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(10)));
    options.AddPolicy("Expire20", builder => builder.Expire(TimeSpan.FromSeconds(20)));
    options.AddPolicy("Expire30", builder => builder.Expire(TimeSpan.FromSeconds(30)));
});

builder.Services.AddHttpClient(
    "blobs",
    client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["AppBaseUrl"]!);
    }
).AddStandardResilienceHandler();

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetConnectionString("StorageAccount"));
});

builder.UseOpenTelemetry(enableAzureMonitor: true, enableAspireDashboard: false);
builder.Services.AddSingleton<AppMetricsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
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
