using System.Net;
using System.Text.Json;
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
        var username = AuthHelper.GetAuthenticatedUser(req, _jwtHelper);
        if (username is null)
        {
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Book id is required." });
            return badRequest;
        }

        UpdateTagsRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<UpdateTagsRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid JSON payload." });
            return badRequest;
        }

        if (payload?.Tags is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "tags is required." });
            return badRequest;
        }

        var normalizedTags = TagNormalization.NormalizeTags(payload.Tags);

        if (normalizedTags.Count > TagNormalization.MaxTagCount)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = $"A maximum of {TagNormalization.MaxTagCount} tags is allowed." });
            return badRequest;
        }

        if (normalizedTags.Any(t => t.Length > TagNormalization.MaxTagLength))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = $"Each tag must be {TagNormalization.MaxTagLength} characters or fewer." });
            return badRequest;
        }

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        var existing = await tableClient.GetEntityIfExistsAsync<BookMetadata>("BOOK", id);
        if (!existing.HasValue)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Book not found." });
            return notFound;
        }

        var entity = existing.Value;
        if (entity is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Book not found." });
            return notFound;
        }

        entity.Tags = string.Join("|", normalizedTags);

        try
        {
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new { error = "Book was modified by another request. Please retry." });
            return conflict;
        }

        _logger.LogInformation("User {User} updated tags for book {BookId}", username, id);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { id, tags = normalizedTags });
        return response;
    }

    private class UpdateTagsRequest
    {
        public List<string>? Tags { get; set; }
    }
}