using AndreGoepel.Marten.Identity.Http;

namespace AndreGoepel.Marten.Identity.Tests.Http;

public class LocalUrlTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/admin/content")]
    [InlineData("/path?query=1&x=2")]
    [InlineData("/path#fragment")]
    public void IsLocal_RootedPaths_ReturnsTrue(string url)
    {
        Assert.True(LocalUrl.IsLocal(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dashboard")] // not rooted
    [InlineData("https://evil.example/phish")] // absolute
    [InlineData("http://evil.example")]
    [InlineData("//evil.example")] // protocol-relative
    [InlineData("//evil.example/path")]
    [InlineData("/\\evil.example")] // backslash trick
    [InlineData("\\\\evil.example")]
    [InlineData("javascript:alert(1)")]
    public void IsLocal_OffSiteOrMalformed_ReturnsFalse(string? url)
    {
        Assert.False(LocalUrl.IsLocal(url));
    }

    [Fact]
    public void OrDefault_LocalUrl_ReturnsIt()
    {
        Assert.Equal("/admin", LocalUrl.OrDefault("/admin", "/dashboard"));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData(null)]
    public void OrDefault_UnsafeUrl_ReturnsFallback(string? url)
    {
        Assert.Equal("/dashboard", LocalUrl.OrDefault(url, "/dashboard"));
    }
}
