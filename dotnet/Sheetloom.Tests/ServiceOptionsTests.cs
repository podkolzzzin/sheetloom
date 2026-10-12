using Microsoft.Extensions.Configuration;

namespace Sheetloom.Tests;

public sealed class ServiceOptionsTests
{
    [Theory]
    [InlineData("PORT", "0")]
    [InlineData("NODE_PORT", "65536")]
    [InlineData("RENDER_CONCURRENCY", "33")]
    [InlineData("RENDER_TIMEOUT_MS", "0")]
    [InlineData("BODY_LIMIT_BYTES", "-1")]
    public void InvalidConfigurationFailsBeforeStartingChildProcess(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [key] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => ServiceOptions.Read(config));
    }

    [Fact]
    public void PublicAndPrivatePortsCannotCollide()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["NODE_PORT"] = "8080" }).Build();
        Assert.Throws<InvalidOperationException>(() => ServiceOptions.Read(config));
    }
}
