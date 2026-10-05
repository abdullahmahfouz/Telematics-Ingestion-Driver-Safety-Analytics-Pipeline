using System.Reflection;
using TelematicsPipeline.Api.Controllers;

namespace TelematicsPipeline.Api.Tests.Controllers;

public class TelematicsControllerLoggingTests
{
    [Fact]
    public void SanitizeForLog_RemovesCrAndLfCharacters()
    {
        var sanitizeForLog = typeof(TelematicsController).GetMethod(
            "SanitizeForLog",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(sanitizeForLog);

        var sanitized = (string?)sanitizeForLog.Invoke(null, ["device\r\n-a\n\r\n"]);

        Assert.Equal("device-a", sanitized);
    }
}
