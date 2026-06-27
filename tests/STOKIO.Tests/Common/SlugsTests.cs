using STOKIO.Application.Common;

namespace STOKIO.Tests.Common;

public sealed class SlugsTests
{
    [Theory]
    [InlineData(" Talha Store ", "talha-store")]
    [InlineData("ABC---123", "abc---123")]
    [InlineData("Kozmetik & Kirtasiye", "kozmetik-kirtasiye")]
    public void Normalize_ReturnsSafeTenantSlug(string value, string expected)
    {
        Assert.Equal(expected, Slugs.Normalize(value));
    }
}

