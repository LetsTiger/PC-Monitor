using PcMonitor.Core.Models;
namespace PcMonitor.Tests;
public sealed class ReleaseInfoTests
{
    [Fact]
    public void ReleaseMetadata_IsSet()
    {
        Assert.Equal("PC Monitor", ReleaseInfo.ProductName);
        Assert.Equal("0.0.1-beta", ReleaseInfo.ProductVersion);
        Assert.Equal("LetsTiger", ReleaseInfo.Publisher);
        Assert.False(string.IsNullOrWhiteSpace(ReleaseInfo.DatabaseSchemaVersion));
    }
}
