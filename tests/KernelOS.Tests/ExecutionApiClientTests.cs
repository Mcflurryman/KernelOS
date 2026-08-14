using System.Net;
using System.Text;
using KernelOS.Web.Execution;

namespace KernelOS.Tests;

public sealed class ExecutionApiClientTests
{
    [Fact]
    public async Task ApprovePostsOnceAndMapsSuccess()
    {
        var id = Guid.NewGuid(); var calls = 0;
        using var handler = new DelegateHttpMessageHandler(async request => { calls++; Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("{\"decision\":\"approve\"}", await request.Content!.ReadAsStringAsync()); return Json(HttpStatusCode.OK, "{\"status\":\"Approved\",\"transitioned\":true}"); });
        var result = await Client(handler).ApproveAsync(id);
        Assert.True(result.IsSuccess); Assert.Equal("Approved", result.Value!.Status); Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ExecutionApiStatus.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ExecutionApiStatus.Conflict)]
    [InlineData((HttpStatusCode)499, ExecutionApiStatus.Cancelled)]
    [InlineData(HttpStatusCode.InternalServerError, ExecutionApiStatus.ServerError)]
    public async Task ExecuteMapsTerminalHttpStates(HttpStatusCode code, ExecutionApiStatus expected)
    {
        using var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(Json(code, "{\"status\":\"Failed\",\"completedTaskCount\":0,\"totalTaskCount\":1}")));
        Assert.Equal(expected, (await Client(handler).ExecuteAsync(Guid.NewGuid())).Status);
    }

    [Fact]
    public async Task RejectMapsNetworkAndInvalidPayloadWithoutRetry()
    {
        var calls = 0; using var network = new DelegateHttpMessageHandler(_ => { calls++; throw new HttpRequestException(); });
        Assert.Equal(ExecutionApiStatus.NetworkUncertain, (await Client(network).RejectAsync(Guid.NewGuid())).Status); Assert.Equal(1, calls);
        using var malformed = new DelegateHttpMessageHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, "not-json")));
        Assert.Equal(ExecutionApiStatus.InvalidPayload, (await Client(malformed).RejectAsync(Guid.NewGuid())).Status);
    }
    private static ExecutionApiClient Client(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("https://kernelos.test/") });
    private static HttpResponseMessage Json(HttpStatusCode code, string value) => new(code) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
}
