using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KernelOS.Tests;

public sealed class UiHostingEndpointTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory factory;

    public UiHostingEndpointTests(TestApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UiRootServesHtml()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/ui");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("<base href=\"/ui/\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UiConversationRouteServesSpaFallback()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/ui/conversations/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ConversationApiRouteIsNotCapturedBySpaFallback()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/conversations/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HealthEndpointRemainsAvailable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task BlazorFrameworkAssetIsServedFromUiBasePath()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ui/_framework/blazor.webassembly.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType!.MediaType!, StringComparison.OrdinalIgnoreCase);
    }
}
