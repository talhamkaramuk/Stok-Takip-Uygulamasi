using STOKIO.Infrastructure.Security;

namespace STOKIO.Tests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForOriginalPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("StrongPass123");

        Assert.True(hasher.Verify("StrongPass123", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("StrongPass123");

        Assert.False(hasher.Verify("WrongPass123", hash));
    }
}

