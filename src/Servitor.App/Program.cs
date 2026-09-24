using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Servitor.App;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Storage;
using Servitor.App.Shared.Files;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IRulesDataStore, IndexedDbRulesDataStore>();
builder.Services.AddScoped<IMergedRulesDataProvider, MergedRulesDataProvider>();
builder.Services.AddScoped<IDatasheetCatalog, DatasheetCatalog>();
builder.Services.AddScoped<IRulesDataMergeService, RulesDataMergeService>();
builder.Services.AddScoped<IFileDownloadService, JsFileDownloadService>();
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
