using WinDesign.Infrastructure;

namespace WinDesign.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesSaltedIterationFormat()
    {
        var hash = PasswordHasher.Hash("admin123");
        var parts = hash.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.True(iterations > 0);
        Assert.NotEmpty(parts[1]);
        Assert.NotEmpty(parts[2]);
    }

    [Fact]
    public void Hash_SamePassword_ProducesDifferentHashes()
    {
        var first = PasswordHasher.Hash("admin123");
        var second = PasswordHasher.Hash("admin123");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("admin123");

        Assert.True(PasswordHasher.Verify("admin123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("admin123");

        Assert.False(PasswordHasher.Verify("admin124", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("1.2")]
    [InlineData("abc.def.ghi")]
    public void Verify_MalformedHash_ReturnsFalse(string malformed)
    {
        Assert.False(PasswordHasher.Verify("admin123", malformed));
    }
}
