using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using JaxaRainmap;
using JaxaRainmap.Services;
using JaxaRainmap.Services.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Logging: in-memory ring buffer + console output (visible in dotnet run terminal)
var logBuffer = new LogBuffer(capacity: 500);
builder.Services.AddSingleton(logBuffer);
builder.Logging.SetMinimumLevel(
    builder.HostEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Warning);
builder.Logging.AddProvider(new InMemoryLoggerProvider(logBuffer));

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IGsmapService, GsmapService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

var app = builder.Build();

// Initialize JS→C# log bridge
JsLogBridge.Initialize(app.Services.GetRequiredService<ILoggerFactory>());

await app.RunAsync();
