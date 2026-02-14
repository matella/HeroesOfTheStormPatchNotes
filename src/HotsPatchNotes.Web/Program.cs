using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HotsPatchNotes.Web;
using HotsPatchNotes.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to point to API
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "http://localhost:5001";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Register services
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IPatchService, PatchService>();
builder.Services.AddScoped<IBattlegroundService, BattlegroundService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IErrorStateService, ErrorStateService>();

await builder.Build().RunAsync();
