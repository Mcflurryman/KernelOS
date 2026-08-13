using KernelOS.Web.Conversations;
using KernelOS.Web.SystemStatus;
using KernelOS.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "/")
});
builder.Services.AddScoped<ConversationApiClient>();
builder.Services.AddScoped<ConversationUiNotifier>();
builder.Services.AddScoped<HealthApiClient>();

await builder.Build().RunAsync();
