using TheLibrary.Api.Models;

namespace TheLibrary.Api.Services;

public static class BookResponseMapper
{
    public static List<string> GetTags(BookMetadata entity)
    {
        return TagNormalization.NormalizePipeDelimitedTags(entity.Tags);
    }

    public static object ToResponse(BookMetadata entity, IReadOnlyList<string>? tags = null)
    {
        var blobPaths = BookFormatVariants.Read(entity);
        var formats = BookFormatVariants.GetFormats(blobPaths);
        var normalizedTags = tags is null ? GetTags(entity) : tags.ToList();

        return new
        {
            id = entity.RowKey,
            title = entity.Title,
            author = entity.Author,
            format = entity.Format,
            blobPath = entity.BlobPath,
            formats,
            blobPaths,
            description = entity.Description,
            tags = normalizedTags
        };
    }
}