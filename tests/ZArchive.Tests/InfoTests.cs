using Xunit;

namespace ZArchive.Tests;

public class InfoTests
{
    [Fact]
    public void NativeAbiVersionIsOne()
    {
        Assert.Equal(1, ZArchiveInfo.NativeAbiVersion);
    }

    [Fact]
    public void UpstreamVersionIsReported()
    {
        Assert.Contains("ZArchive", ZArchiveInfo.UpstreamVersion);
    }
}
