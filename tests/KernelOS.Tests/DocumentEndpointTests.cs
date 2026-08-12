using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KernelOS.Tests;

public sealed class DocumentEndpointTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
    [Theory]
    [InlineData("text/sample.txt")]
    [InlineData("markdown/sample.md")]
    [InlineData("json/sample.json")]
    [InlineData("csv/sample.csv")]
    public async Task ReadSupportedDocumentReturnsOk(string relativePath)
    {
        using var response = await factory.CreateClient().PostAsJsonAsync("/documents/read", new { path = $"Workspace/testdata/documents/{relativePath}" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("C:\\Users", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsupportedDocumentReturnsBadRequest()
    {
        using var response = await factory.CreateClient().PostAsJsonAsync("/documents/read", new { path = "Workspace/testdata/documents/corrupt/unsupported.bin" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingDocumentReturnsNotFound()
    {
        using var response = await factory.CreateClient().PostAsJsonAsync("/documents/read", new { path = "Workspace/testdata/documents/text/missing.txt" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnauthorizedDocumentReturnsForbidden()
    {
        using var response = await factory.CreateClient().PostAsJsonAsync("/documents/read", new { path = "C:\\Windows\\System32\\config\\SAM" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EmptyBodyReturnsBadRequest()
    {
        using var response = await factory.CreateClient().PostAsync("/documents/read", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PromptInjectionIsReturnedOnlyAsDocumentData()
    {
        using var response = await factory.CreateClient().PostAsJsonAsync("/documents/read", new { path = "Workspace/testdata/documents/markdown/prompt-injection.md" });
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ignore previous instructions", body, StringComparison.Ordinal);
    }
}
