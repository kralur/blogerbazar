using BloggerBazar.Infrastructure.Security;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

public sealed class AdminWebOptionsTests
{
    [Theory]
    [InlineData("https://admin-web-production.up.railway.app", "https://admin-web-production.up.railway.app")]
    [InlineData("https://Admin.Example.com/", "https://admin.example.com")]
    [InlineData(" https://admin.example.com:8443 ", "https://admin.example.com:8443")]
    [InlineData("http://localhost:5174", "http://localhost:5174")]
    [InlineData("http://127.0.0.1:5174", "http://127.0.0.1:5174")]
    public void Keeps_only_the_site_address(string value, string expected)
    {
        Assert.Equal(expected, AdminWebOptions.NormalizeOrigin(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("admin.example.com")]
    [InlineData("http://admin.example.com")]
    [InlineData("https://admin.example.com/login")]
    [InlineData("https://admin.example.com/?x=1")]
    [InlineData("https://user:pass@admin.example.com")]
    [InlineData("ftp://admin.example.com")]
    public void Anything_else_keeps_the_web_sign_in_off(string? value)
    {
        Assert.Null(AdminWebOptions.NormalizeOrigin(value));
        Assert.Null(new AdminWebOptions { Origin = value ?? string.Empty }.NormalizedOrigin);
    }
}
