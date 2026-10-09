using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Servitor.App;
using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.ArmyCollection.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Storage;
using Servitor.App.RulesData.Updating;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Shared.Backdrop;
using Servitor.App.Shared.Effects;
using Servitor.App.Shared.Files;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IRulesDataStore, IndexedDbRulesDataStore>();
builder.Services.AddScoped<IMergedRulesDataProvider, MergedRulesDataProvider>();
builder.Services.AddScoped<IDatasheetCatalog, DatasheetCatalog>();
builder.Services.AddScoped<IDetachmentRuleProvider, HttpDetachmentRuleProvider>();
builder.Services.AddScoped<IArmyBuilderState, ArmyBuilderState>();
builder.Services.AddScoped<IMissionDeckProvider, HttpMissionDeckProvider>();
builder.Services.AddScoped<IBattleSetupState, BattleSetupState>();
builder.Services.AddScoped<IArmyCollectionStore, ArmyCollectionStore>();
builder.Services.AddScoped<IRulesDataMergeService, RulesDataMergeService>();
builder.Services.AddScoped<IDataUpdateService, HttpDataUpdateService>();
builder.Services.AddScoped<IFileDownloadService, JsFileDownloadService>();
builder.Services.AddScoped<IFactionBackdrop, FactionBackdrop>();
builder.Services.AddScoped<ICrtEffectsService, JsCrtEffectsService>();
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
