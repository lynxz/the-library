using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class UpdateBookTags
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<UpdateBookTags> _logger;

    public UpdateBookTags(TableServiceClient tableServiceClient, JwtHelper jwtHelper, ILogger<UpdateBookTags> logger)
    {
        _tableServiceClient = tableServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    [Function("updateBookTags")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "patch", Route = "bookTags/{id}")] HttpRequestData req,
        string id)
    {
        var (username, guardError) = await AuthHelper.RequireAuthenticatedUser(req, _jwtHelper);
        if (guardError is not null)
        {
            return guardError;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return await FunctionResponses.Error(req, HttpStatusCode.BadRequest, "Book id is required.");
        }

        var (payload, parseError) = await FunctionRequests.TryReadJsonAsync<UpdateTagsRequest>(req, "Invalid JSON payload.");
        if (parseError is not null)
        {
            return parseError;
        }

        if (payload?.Tags is null)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.BadRequest, "tags is required.");
        }

        var normalizedTags = TagNormalization.NormalizeTags(payload.Tags);

        if (normalizedTags.Count > TagNormalization.MaxTagCount)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.BadRequest, $"A maximum of {TagNormalization.MaxTagCount} tags is allowed.");
        }

        if (normalizedTags.Any(t => t.Length > TagNormalization.MaxTagLength))
        {
            return await FunctionResponses.Error(req, HttpStatusCode.BadRequest, $"Each tag must be {TagNormalization.MaxTagLength} characters or fewer.");
        }

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        var existing = await tableClient.GetEntityIfExistsAsync<BookMetadata>("BOOK", id);
        if (!existing.HasValue)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.NotFound, "Book not found.");
        }

        var entity = existing.Value;
        if (entity is null)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.NotFound, "Book not found.");
        }

        entity.Tags = string.Join("|", normalizedTags);

        try
        {
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return await FunctionResponses.Error(req, HttpStatusCode.Conflict, "Book was modified by another request. Please retry.");
        }

        _logger.LogInformation("User {User} updated tags for book {BookId}", username, id);

        return await FunctionResponses.Json(req, HttpStatusCode.OK, new { id, tags = normalizedTags });
    }

    private class UpdateTagsRequest
    {
        public List<string>? Tags { get; set; }
    }
}