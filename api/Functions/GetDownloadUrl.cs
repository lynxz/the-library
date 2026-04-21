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
        var username = AuthHelper.GetAuthenticatedUser(req, _jwtHelper);
        if (username is null)
        {
            var unauthorized = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var blobPath = query["blobPath"];

        if (string.IsNullOrWhiteSpace(blobPath))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "blobPath query parameter is required." });
            return badRequest;
        }

        // Sanitize: reject path traversal and absolute paths
        if (blobPath.Contains("..") || Path.IsPathRooted(blobPath) || blobPath.Contains('\\'))
        {
            var forbidden = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await forbidden.WriteAsJsonAsync(new { error = "Invalid blob path." });
            return forbidden;
        }

        _logger.LogInformation("Generating download URL for blob: {BlobPath}", blobPath);

        var containerClient = _blobServiceClient.GetBlobContainerClient("books");
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Book not found." });
            return notFound;
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

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { url = sasUri.ToString() });
        return response;
    }
}
