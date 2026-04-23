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
        var (_, guardError) = await AuthHelper.RequireAuthenticatedUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        _logger.LogInformation("Listing books from table storage");

        var query = HttpUtility.ParseQueryString(req.Url.Query);
        var requiredTags = TagNormalization.NormalizeCsvTags(query["tags"]);

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        var books = new List<object>();

        await foreach (var entity in tableClient.QueryAsync<BookMetadata>())
        {
            var tags = BookResponseMapper.GetTags(entity);

            if (requiredTags.Count > 0 && !requiredTags.All(tags.Contains))
            {
                continue;
            }

            books.Add(BookResponseMapper.ToResponse(entity, tags));
        }

        return await FunctionResponses.Json(req, System.Net.HttpStatusCode.OK, books);
    }
}
