using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorWebApp.Pages;

public class BlobModel : PageModel
{
    private readonly ILogger<BlobModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public string HtmlContent { get; private set; } = string.Empty;

    public BlobModel(
        ILogger<BlobModel> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnGet()
    {
        var httpClient = _httpClientFactory.CreateClient("blobs");
        HtmlContent = await httpClient.GetStringAsync("/stream-html/web/html-part.html");
    }
}

