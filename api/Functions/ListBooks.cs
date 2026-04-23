using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Web;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class ListBooks
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<ListBooks> _logger;

    public ListBooks(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<ListBooks> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    [Function("listBooks")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "listBooks")] HttpRequestData req)
    {
        var username = AuthHelper.GetAuthenticatedUser(req, _jwtHelper);
        if (username is null)
        {
            var unauthorized = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        _logger.LogInformation("Listing books from table storage");

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var requiredTags = TagNormalization.NormalizeCsvTags(query["tags"]);

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        var books = new List<object>();

        await foreach (var entity in tableClient.QueryAsync<BookMetadata>())
        {
            var tags = TagNormalization.NormalizePipeDelimitedTags(entity.Tags);
            var blobPaths = BookFormatVariants.Read(entity);
            var formats = BookFormatVariants.GetFormats(blobPaths);

            if (requiredTags.Count > 0 && !requiredTags.All(tags.Contains))
            {
                continue;
            }

            books.Add(new
            {
                id = entity.RowKey,
                title = entity.Title,
                author = entity.Author,
                format = entity.Format,
                blobPath = entity.BlobPath,
                formats,
                blobPaths,
                description = entity.Description,
                tags
            });
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(books);
        return response;
    }
}
