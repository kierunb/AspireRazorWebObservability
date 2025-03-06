using Azure.Storage.Blobs;
using Microsoft.AspNetCore.OutputCaching;

namespace RazorWebApp.Endpoints;

public static class BlobEndpoint
{
    public static void MapBlobEndpoints(this WebApplication app)
    {
        // remove [OutputCache] before async keyword to disable output caching

        app.MapGet(
            "/stream-image/{containerName}/{blobName}",
            [OutputCache(PolicyName = "Expire20")] async (
                string blobName,
                string containerName,
                BlobServiceClient blobServiceClient,
                CancellationToken token
            ) =>
            {
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);

                
                app.Logger.LogInformation(
                    "Fetching blob name: {blobName} from container: {container}",
                    blobName,
                    containerName
                );

                return Results.Stream(
                    await blobClient.OpenReadAsync(cancellationToken: token),
                    contentType: "image/jpeg"
                );
            }
        );

        app.MapGet(
            "/stream-html/{containerName}/{blobName}",
            [OutputCache] async (
                string blobName,
                string containerName,
                BlobServiceClient blobServiceClient,
                CancellationToken token
            ) =>
            {
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(blobName);

                app.Logger.LogInformation(
                    "Fetching blob name: {blobName} from container: {container}",
                    blobName,
                    containerName
                );

                return Results.Stream(
                    await blobClient.OpenReadAsync(cancellationToken: token),
                    contentType: "text/html"
                );
            }
        );
    }
}
