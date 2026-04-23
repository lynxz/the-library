using System.Text.Json;
using TheLibrary.Api.Models;

namespace TheLibrary.Api.Services;

public static class BookFormatVariants
{
    private static readonly string[] FormatOrder = ["PDF", "EPUB"];

    public static string? GetFormatFromExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "PDF",
            ".epub" => "EPUB",
            _ => null
        };
    }

    public static Dictionary<string, string> Read(BookMetadata entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.FormatBlobPaths))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.FormatBlobPaths);
                if (parsed is not null)
                {
                    var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in parsed)
                    {
                        var normalizedFormat = NormalizeFormat(pair.Key);
                        if (normalizedFormat is null || string.IsNullOrWhiteSpace(pair.Value))
                        {
                            continue;
                        }

                        normalized[normalizedFormat] = pair.Value;
                    }

                    if (normalized.Count > 0)
                    {
                        return normalized;
                    }
                }
            }
            catch
            {
                // Fall through to legacy fields.
            }
        }

        var legacy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var legacyFormat = NormalizeFormat(entity.Format);
        if (legacyFormat is not null && !string.IsNullOrWhiteSpace(entity.BlobPath))
        {
            legacy[legacyFormat] = entity.BlobPath;
        }

        return legacy;
    }

    public static void Write(BookMetadata entity, IReadOnlyDictionary<string, string> variants)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in variants)
        {
            var normalizedFormat = NormalizeFormat(pair.Key);
            if (normalizedFormat is null || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            normalized[normalizedFormat] = pair.Value;
        }

        entity.FormatBlobPaths = JsonSerializer.Serialize(normalized);

        var primary = GetPrimaryFormat(normalized);
        if (primary is null)
        {
            entity.Format = string.Empty;
            entity.BlobPath = string.Empty;
            return;
        }

        entity.Format = primary;
        entity.BlobPath = normalized[primary];
    }

    public static List<string> GetFormats(IReadOnlyDictionary<string, string> variants)
    {
        var existing = new HashSet<string>(variants.Keys.Select(k => NormalizeFormat(k)).Where(k => k is not null)!, StringComparer.OrdinalIgnoreCase);
        return FormatOrder.Where(existing.Contains).ToList();
    }

    public static string? GetPrimaryFormat(IReadOnlyDictionary<string, string> variants)
    {
        foreach (var format in FormatOrder)
        {
            if (variants.ContainsKey(format))
            {
                return format;
            }
        }

        return null;
    }

    private static string? NormalizeFormat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToUpperInvariant() switch
        {
            "PDF" => "PDF",
            "EPUB" => "EPUB",
            _ => null
        };
    }
}