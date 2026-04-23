using Azure;
using Azure.Data.Tables;

namespace TheLibrary.Api.Models;

public class BookMetadata : ITableEntity
{
    public string PartitionKey { get; set; } = "BOOK";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty; // "PDF" or "EPUB"
    public string BlobPath { get; set; } = string.Empty;
    public string? FormatBlobPaths { get; set; }
    public string? Description { get; set; }
    public string Tags { get; set; } = string.Empty;
}
