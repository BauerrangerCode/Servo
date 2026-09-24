using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Servitor.DataUpdate.Wahapedia;

public static partial class Slugs
{
    public static string Slugify(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }

        var decomposed = input.Normalize(NormalizationForm.FormKD);
        var withoutMarks = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return NonSlugRegex().Replace(withoutMarks.ToLowerInvariant(), "-").Trim('-');
    }

    /// <summary>Creates slugs that stay unique: a repeated base gets "-2", "-3", ... appended.</summary>
    public static Func<string, string> CreateGenerator(string fallback = "item")
    {
        var counts = new Dictionary<string, int>();
        return value =>
        {
            var slug = Slugify(value);
            var baseSlug = slug.Length > 0 ? slug : fallback;
            var current = counts.GetValueOrDefault(baseSlug);
            counts[baseSlug] = current + 1;
            return current == 0 ? baseSlug : $"{baseSlug}-{current + 1}";
        };
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugRegex();
}
