using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class GetDownloadUrl
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<GetDownloadUrl> _logger;

    public GetDownloadUrl(BlobServiceClient blobServiceClient, JwtHelper jwtHelper, ILogger<GetDownloadUrl> logger)
    {
        _blobServiceClient = blobServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    [Function("getDownloadUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "getDownloadUrl")] HttpRequestData req)
    {
        var (_, guardError) = await AuthHelper.RequireAuthenticatedUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var blobPath = query["blobPath"];

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "blobPath query parameter is required.");
        }

        // Sanitize: reject path traversal and absolute paths
        if (blobPath.Contains("..") || Path.IsPathRooted(blobPath) || blobPath.Contains('\\'))
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.BadRequest, "Invalid blob path.");
        }

        _logger.LogInformation("Generating download URL for blob: {BlobPath}", blobPath);

        var containerClient = _blobServiceClient.GetBlobContainerClient("books");
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync())
        {
            return await FunctionResponses.Error(req, System.Net.HttpStatusCode.NotFound, "Book not found.");
        }

        // Generate a SAS token valid for 5 minutes (read-only)
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = "books",
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.OK, new { url = sasUri.ToString() });
    }
}
