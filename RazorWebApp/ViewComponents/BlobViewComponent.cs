using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using RazorWebApp.Services;
using System.Drawing;
using System.Text;

namespace RazorWebApp.ViewComponents;

public class BlobViewComponent : ViewComponent
{
    private readonly ILogger<BlobViewComponent> _logger;
    private readonly BlobService _blobService;

    public BlobViewComponent(
        ILogger<BlobViewComponent> logger,
        BlobService blobService)
    {
        _logger = logger;
        _blobService = blobService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string containerName, string blobName)
    {
    
        //var htmlChunks = await _blobService.GetBlobChunksAsync(
        //    containerName,
        //    blobName
        //);

        var chunk = await _blobService.GetBlobAsync(
            containerName,
            blobName
        );
        List<string> htmlChunks = [chunk];

        return View("Default", htmlChunks.ToArray());
    }

}
