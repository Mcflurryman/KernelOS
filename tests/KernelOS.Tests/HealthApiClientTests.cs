using System.Net;
using System.Text;
using KernelOS.Web.SystemStatus;

namespace KernelOS.Tests;

public sealed class HealthApiClientTests
{
    [Fact]
    public async Task GetKernelApiAsyncMapsOkToOnline()
    {
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            Assert.Equal("https://kernelos.test/health", request.RequestUri!.ToString());
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"status\":\"ok\"}"));
        });

        var result = await CreateClient(handler).GetKernelApiAsync();

        Assert.Equal(HealthStatus.Online, result.Status);
    }

    [Fact]
    public async Task GetOllamaAsyncMapsServiceUnavailableToOffline()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await CreateClient(handler).GetOllamaAsync();

        Assert.Equal(HealthStatus.Offline, result.Status);
    }

    [Fact]
    public async Task GetKernelApiAsyncMapsNetworkFailureToOffline()
    {
        using var handler = new DelegateHttpMessageHandler(_ => throw new HttpRequestException());

        var result = await CreateClient(handler).GetKernelApiAsync();

        Assert.Equal(HealthStatus.Offline, result.Status);
    }

    [Fact]
    public async Task GetKernelApiAsyncMapsInvalidJsonToDegraded()
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, "not-json")));

        var result = await CreateClient(handler).GetKernelApiAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private static HealthApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://kernelos.test/") });

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
