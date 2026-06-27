using System.Text.RegularExpressions;

namespace STOKIO.Application.Common;

public static partial class Slugs
{
    public static string Normalize(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = TenantSlugRegex().Replace(slug, "-");
        slug = slug.Trim('-');
        return slug;
    }

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.Compiled)]
    private static partial Regex TenantSlugRegex();
}

