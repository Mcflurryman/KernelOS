using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace KernelOS.Tests;
public sealed class FilesystemEndpointTests(TestApplicationFactory factory) : IClassFixture<TestApplicationFactory>
{
 [Fact] public async Task InvalidFilesystemOperationReturnsBadRequest(){using var r=await factory.CreateClient().PostAsync("/filesystem/invalid",JsonContent.Create(new{arguments=new{path="Workspace"}}));Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);}
 [Fact] public async Task FilesystemOperationDoesNotRequireOperationInBody(){using var r=await factory.CreateClient().PostAsync("/filesystem/exists",JsonContent.Create(new{arguments=new{path="Workspace/testdata/filesystem/sample.cs"}}));Assert.Equal(HttpStatusCode.OK,r.StatusCode);}
 [Fact] public async Task UnauthorizedPathReturnsForbidden(){using var r=await factory.CreateClient().PostAsync("/filesystem/metadata",JsonContent.Create(new{arguments=new{path="C:\\Windows\\System32\\config\\SAM"}}));Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);}
 [Fact] public async Task MissingArgumentsReturnBadRequest(){using var r=await factory.CreateClient().PostAsync("/filesystem/exists",JsonContent.Create(new { }));Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);}
 [Fact] public async Task MissingAuthorizedEntryReturnsNotFound(){using var r=await factory.CreateClient().PostAsync("/filesystem/metadata",JsonContent.Create(new{arguments=new{path="Workspace/testdata/filesystem/missing.txt"}}));Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);}
 [Fact] public async Task SuccessfulSearchReturnsOk(){using var r=await factory.CreateClient().PostAsync("/filesystem/search",JsonContent.Create(new{arguments=new{path="Workspace/testdata/filesystem",pattern="*.cs"}}));Assert.Equal(HttpStatusCode.OK,r.StatusCode);}
}
