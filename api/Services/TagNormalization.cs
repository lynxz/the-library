namespace TheLibrary.Api.Services;

public static class TagNormalization
{
    public const int MaxTagCount = 10;
    public const int MaxTagLength = 24;

    public static List<string> NormalizeCsvTags(string? rawTags)
    {
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            return new List<string>();
        }

        return rawTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> NormalizePipeDelimitedTags(string? rawTags)
    {
        if (string.IsNullOrWhiteSpace(rawTags))
        {
            return new List<string>();
        }

        return rawTags
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeTag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .Select(t => t ?? string.Empty)
            .Select(NormalizeTag)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static string NormalizeTag(string input)
    {
        var lowered = input.ToLowerInvariant().Trim();
        var chars = lowered.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '+' || c == '#').ToArray();
        return new string(chars);
    }
}