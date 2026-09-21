using TelematicsPipeline.Api.Auth;
using Xunit;

namespace TelematicsPipeline.Api.Tests.Auth;

public class DeviceApiKeyGeneratorTests
{
    [Fact]
    public void Generate_EmbedsTheDeviceIdForReadability()
    {
        var key = DeviceApiKeyGenerator.Generate("b2A83F1");

        Assert.StartsWith("dak_b2A83F1_", key);
    }

    [Fact]
    public void Generate_ProducesADifferentKeyEachTime()
    {
        var first = DeviceApiKeyGenerator.Generate("device-a");
        var second = DeviceApiKeyGenerator.Generate("device-a");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Hash_IsDeterministic_SoALookupByHashWorks()
    {
        var key = DeviceApiKeyGenerator.Generate("device-a");

        Assert.Equal(DeviceApiKeyGenerator.Hash(key), DeviceApiKeyGenerator.Hash(key));
    }

    [Fact]
    public void Hash_NeverContainsTheOriginalKey()
    {
        var key = DeviceApiKeyGenerator.Generate("device-a");

        Assert.DoesNotContain(key, DeviceApiKeyGenerator.Hash(key));
    }

    [Fact]
    public void Hash_DiffersBetweenDifferentKeys()
    {
        var first = DeviceApiKeyGenerator.Generate("device-a");
        var second = DeviceApiKeyGenerator.Generate("device-b");

        Assert.NotEqual(DeviceApiKeyGenerator.Hash(first), DeviceApiKeyGenerator.Hash(second));
    }
}
