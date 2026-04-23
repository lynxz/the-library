using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TheLibrary.Api.Models;
using TheLibrary.Api.Services;

namespace TheLibrary.Api.Functions;

public class UploadBook
{
    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

    private static readonly Dictionary<string, (string Format, string ContentType)> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("PDF", "application/pdf"),
        [".epub"] = ("EPUB", "application/epub+zip")
    };

    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly JwtHelper _jwtHelper;
    private readonly ILogger<UploadBook> _logger;

    public UploadBook(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient, JwtHelper jwtHelper, ILogger<UploadBook> logger)
    {
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
        _jwtHelper = jwtHelper;
        _logger = logger;
    }

    [Function("uploadBook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploadBook")] HttpRequestData req)
    {
        var username = AuthHelper.GetAuthenticatedUser(req, _jwtHelper);
        if (username is null)
        {
            var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
            await unauthorized.WriteAsJsonAsync(new { error = "Not authenticated." });
            return unauthorized;
        }

        // Parse the multipart form data
        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault() ?? "";
        if (!contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Content-Type must be multipart/form-data." });
            return badRequest;
        }

        var boundary = GetBoundary(contentType);
        if (boundary is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Invalid multipart boundary." });
            return badRequest;
        }

        var parts = await ParseMultipartAsync(req.Body, boundary);

        var bookId = parts.Fields.GetValueOrDefault("bookId")?.Trim();
        var replaceExisting = string.Equals(parts.Fields.GetValueOrDefault("replaceExisting"), "true", StringComparison.OrdinalIgnoreCase);
        var title = parts.Fields.GetValueOrDefault("title")?.Trim();
        var author = parts.Fields.GetValueOrDefault("author")?.Trim();
        var description = parts.Fields.GetValueOrDefault("description")?.Trim();
        var tags = TagNormalization.NormalizeCsvTags(parts.Fields.GetValueOrDefault("tags"));

        if (parts.FileName is null || parts.FileContent is null || parts.FileContent.Length == 0)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "A book file is required." });
            return badRequest;
        }

        if (tags.Count > TagNormalization.MaxTagCount)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = $"A maximum of {TagNormalization.MaxTagCount} tags is allowed." });
            return badRequest;
        }

        if (tags.Any(t => t.Length > TagNormalization.MaxTagLength))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = $"Each tag must be {TagNormalization.MaxTagLength} characters or fewer." });
            return badRequest;
        }

        if (parts.FileContent.Length > MaxFileSize)
        {
            var tooLarge = req.CreateResponse(HttpStatusCode.RequestEntityTooLarge);
            await tooLarge.WriteAsJsonAsync(new { error = "File size exceeds the 50 MB limit." });
            return tooLarge;
        }

        var extension = Path.GetExtension(parts.FileName);
        if (!AllowedExtensions.TryGetValue(extension, out var formatInfo))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Only .pdf and .epub files are allowed." });
            return badRequest;
        }

        var tableClient = _tableServiceClient.GetTableClient("BookMetadata");
        await tableClient.CreateIfNotExistsAsync();

        if (!string.IsNullOrWhiteSpace(bookId))
        {
            var existing = await tableClient.GetEntityIfExistsAsync<BookMetadata>("BOOK", bookId);
            if (!existing.HasValue || existing.Value is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(new { error = "Book not found." });
                return notFound;
            }

            var existingEntity = existing.Value;
            var variants = BookFormatVariants.Read(existingEntity);

            var isReplacing = variants.TryGetValue(formatInfo.Format, out var existingBlobPath);
            if (isReplacing && !replaceExisting)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteAsJsonAsync(new
                {
                    error = $"{formatInfo.Format} already exists for this book. Confirm replacement to overwrite it.",
                    requiresConfirmation = true,
                    format = formatInfo.Format
                });
                return conflict;
            }

            var targetBlobPath = isReplacing
                ? existingBlobPath!
                : $"{ToFileNameBase(existingEntity.Title)}{extension.ToLowerInvariant()}";

            _logger.LogInformation("{Action} format {Format} for book {BookId} at {BlobPath}", isReplacing ? "Replacing" : "Adding", formatInfo.Format, bookId, targetBlobPath);

            var addContainerClient = _blobServiceClient.GetBlobContainerClient("books");
            await addContainerClient.CreateIfNotExistsAsync();
            var addBlobClient = addContainerClient.GetBlobClient(targetBlobPath);

            if (!isReplacing && await addBlobClient.ExistsAsync())
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteAsJsonAsync(new
                {
                    error = $"A book file named '{targetBlobPath}' already exists. Use a different title or remove the existing book first."
                });
                return conflict;
            }

            try
            {
                using var addStream = new MemoryStream(parts.FileContent);
                await addBlobClient.UploadAsync(addStream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = formatInfo.ContentType },
                    Conditions = isReplacing ? null : new BlobRequestConditions { IfNoneMatch = ETag.All }
                });
            }
            catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteAsJsonAsync(new
                {
                    error = $"A book file named '{targetBlobPath}' already exists. Use a different title or remove the existing book first."
                });
                return conflict;
            }

            variants[formatInfo.Format] = targetBlobPath;
            BookFormatVariants.Write(existingEntity, variants);

            try
            {
                await tableClient.UpdateEntityAsync(existingEntity, existingEntity.ETag, TableUpdateMode.Replace);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                await conflict.WriteAsJsonAsync(new { error = "Book was modified by another request. Please retry." });
                return conflict;
            }

            return await FunctionResponses.Json(req, HttpStatusCode.OK, BookResponseMapper.ToResponse(existingEntity));
        }

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(author))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Title and author are required." });
            return badRequest;
        }

        var rowKey = Guid.NewGuid().ToString("N");
        var fileNameBase = ToFileNameBase(title);
        var blobPath = $"{fileNameBase}{extension.ToLowerInvariant()}";

        _logger.LogInformation("Uploading book '{Title}' by {Author} as {BlobPath}", title, author, blobPath);

        // Upload blob
        var containerClient = _blobServiceClient.GetBlobContainerClient("books");
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(blobPath);

        if (await blobClient.ExistsAsync())
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new
            {
                error = $"A book file named '{blobPath}' already exists. Use a different title or remove the existing book first."
            });
            return conflict;
        }

        using var stream = new MemoryStream(parts.FileContent);
        try
        {
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = formatInfo.ContentType });
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteAsJsonAsync(new
            {
                error = $"A book file named '{blobPath}' already exists. Use a different title or remove the existing book first."
            });
            return conflict;
        }

        var entity = new BookMetadata
        {
            PartitionKey = "BOOK",
            RowKey = rowKey,
            Title = title,
            Author = author,
            Format = formatInfo.Format,
            BlobPath = blobPath,
            Description = string.IsNullOrEmpty(description) ? null : description,
            Tags = string.Join("|", tags)
        };
        BookFormatVariants.Write(entity, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [formatInfo.Format] = blobPath
        });

        await tableClient.AddEntityAsync(entity);

        return await FunctionResponses.Json(req, HttpStatusCode.Created, BookResponseMapper.ToResponse(entity));
    }

    private static string ToFileNameBase(string title)
    {
        var lower = title.ToLowerInvariant();
        var chars = lower.Where(char.IsLetterOrDigit).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "book" : sanitized;
    }

    private static string? GetBoundary(string contentType)
    {
        var parts = contentType.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            {
                var boundary = trimmed["boundary=".Length..].Trim('"');
                return boundary;
            }
        }
        return null;
    }

    private static async Task<MultipartData> ParseMultipartAsync(Stream body, string boundary)
    {
        using var ms = new MemoryStream();
        await body.CopyToAsync(ms);
        var data = ms.ToArray();

        var result = new MultipartData();
        var boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary);
        var positions = FindAllOccurrences(data, boundaryBytes);

        for (int i = 0; i < positions.Count - 1; i++)
        {
            var start = positions[i] + boundaryBytes.Length;
            var end = positions[i + 1];

            // Skip \r\n after boundary
            if (start < data.Length && data[start] == '\r') start++;
            if (start < data.Length && data[start] == '\n') start++;

            // Find end of headers (double CRLF)
            var headerEnd = FindSequence(data, System.Text.Encoding.UTF8.GetBytes("\r\n\r\n"), start);
            if (headerEnd < 0) continue;

            var headerText = System.Text.Encoding.UTF8.GetString(data, start, headerEnd - start);
            var contentStart = headerEnd + 4; // skip \r\n\r\n

            // Content ends before \r\n--boundary
            var contentEnd = end - 2; // strip trailing \r\n before next boundary
            if (contentEnd <= contentStart) continue;

            var contentBytes = new byte[contentEnd - contentStart];
            Array.Copy(data, contentStart, contentBytes, 0, contentBytes.Length);

            // Parse the Content-Disposition header
            var namePart = ExtractHeaderValue(headerText, "name");
            var fileNamePart = ExtractHeaderValue(headerText, "filename");

            if (fileNamePart is not null)
            {
                result.FileName = fileNamePart;
                result.FileContent = contentBytes;
            }
            else if (namePart is not null)
            {
                result.Fields[namePart] = System.Text.Encoding.UTF8.GetString(contentBytes);
            }
        }

        return result;
    }

    private static string? ExtractHeaderValue(string headers, string key)
    {
        var search = key + "=\"";
        var idx = headers.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + search.Length;
        var end = headers.IndexOf('"', start);
        return end < 0 ? null : headers[start..end];
    }

    private static List<int> FindAllOccurrences(byte[] data, byte[] pattern)
    {
        var positions = new List<int>();
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) positions.Add(i);
        }
        return positions;
    }

    private static int FindSequence(byte[] data, byte[] sequence, int start)
    {
        for (int i = start; i <= data.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (data[i + j] != sequence[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private class MultipartData
    {
        public Dictionary<string, string> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? FileName { get; set; }
        public byte[]? FileContent { get; set; }
    }
}
