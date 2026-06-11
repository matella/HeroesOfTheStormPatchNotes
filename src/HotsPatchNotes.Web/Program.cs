using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HotsPatchNotes.Web;
using HotsPatchNotes.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to point to API
// Empty/missing ApiBaseAddress → same-origin (the page URL): nginx proxies /api to the API
// container, so the app works from any host (LAN, Tailscale, domain) with zero baked-in URL.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"];
if (string.IsNullOrWhiteSpace(apiBaseAddress))
    apiBaseAddress = builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Register services
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IPatchService, PatchService>();
builder.Services.AddScoped<IBattlegroundService, BattlegroundService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IErrorStateService, ErrorStateService>();

await builder.Build().RunAsync();
