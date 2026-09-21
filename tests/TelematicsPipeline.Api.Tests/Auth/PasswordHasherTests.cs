using TelematicsPipeline.Api.Auth;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForTheOriginalPassword()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");

        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForAWrongPassword()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");

        Assert.False(PasswordHasher.Verify("wrong password", hash));
    }

    [Fact]
    public void Hash_NeverStoresThePasswordInPlaintext()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");

        Assert.DoesNotContain("correct horse battery staple", hash);
    }

    [Fact]
    public void Hash_ProducesADifferentHashEachTime()
    {
        var first = PasswordHasher.Hash("correct horse battery staple");
        var second = PasswordHasher.Hash("correct horse battery staple");

        Assert.NotEqual(first, second);
    }
}
