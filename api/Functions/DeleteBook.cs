using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class DeleteBook
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<DeleteBook> _logger;

    public DeleteBook(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<DeleteBook> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    [Function("deleteBook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "books/{id}")] HttpRequestData req,
        string id)
    {
        var (username, guardError) = await AuthHelper.RequireAdminUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return await FunctionResponses.Error(req, HttpStatusCode.BadRequest, "Book id is required.");
        }

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        var existing = await tableClient.GetEntityIfExistsAsync<TableEntity>("BOOK", id);
        if (!existing.HasValue)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.NotFound, "Book not found.");
        }

        await tableClient.DeleteEntityAsync("BOOK", id);

        _logger.LogInformation("Admin {User} deleted metadata for book {BookId}", username, id);

        return await FunctionResponses.Json(req, HttpStatusCode.OK, new { id });
    }
}