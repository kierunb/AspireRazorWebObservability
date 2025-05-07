using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;

namespace RazorWebApp.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly AppMetricsService _appMetrics;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Random _random = Random.Shared;

    public IndexModel(
        ILogger<IndexModel> logger,
        AppMetricsService appMetrics,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _appMetrics = appMetrics;
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnGet()
    {
        var httpClient = _httpClientFactory.CreateClient();

        string category = "FrontPage";
        
        _logger.LogWarning($"Unstructured Logs. Number: {DateTime.Now.Microsecond}, Date: {DateTime.Now}, Category: {category}");

        _logger.LogWarning("Structured Logs. Number: {number}, Date: {date}, Category: {category}", 
            DateTime.Now.Microsecond, DateTime.Now, category);


        // structred logging (using log templates)
        _logger.LogInformation("Index page visited, number {number}", DateTime.Now.Millisecond);


        AppLogs.LogIndexPageVisited(_logger, "Index page visited");

        // metrics
        _appMetrics.RecordUserClickDetailed(region: "Europe", feature: "Welcome page");
        _appMetrics.PageVisit(1);
        _appMetrics.RecordRequest();
        _appMetrics.RecordMemoryConsumption(GC.GetAllocatedBytesForCurrentThread() / (1024 * 1024));
        _appMetrics.RecordResponseTime(_random.NextDouble());

        // traces/activities
        var response = await httpClient.GetAsync("https://www.bing.com");

        using (Activity? activity = AppActivitySources.AppFrontentActivitySource.StartActivity("Index page activity"))
        {
            activity?.SetTag("page", "index");
            activity?.SetTag("user", "user-1");
            await Task.Delay(100);

        }
    }
}
