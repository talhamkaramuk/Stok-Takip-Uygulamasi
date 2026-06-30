using System.Text;

namespace STOKIO.Infrastructure.Services;

internal static class OperationSearchText
{
    public const int MaxLength = 2048;

    public static string Build(params string?[] values)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(value.Trim());
        }

        return Normalize(builder.ToString());
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhiteSpace = true;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhiteSpace)
                {
                    builder.Append(' ');
                    previousWasWhiteSpace = true;
                }

                continue;
            }

            builder.Append(character);
            previousWasWhiteSpace = false;

            if (builder.Length >= MaxLength)
            {
                break;
            }
        }

        return builder.ToString();
    }
}
