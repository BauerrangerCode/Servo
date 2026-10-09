using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Updating;
using Servitor.App.Shared.Effects;
using Servitor.App.Shared.Files;
using Servitor.App.Shared.Settings;
using Servitor.App.Shared.Shell;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.Shared.Settings;

public class SettingsMenuTests : BunitContext
{
    private sealed class FakeEffects : ICrtEffectsService
    {
        public int LoadCount { get; private set; }

        public Task<CrtEffectsSettings> LoadAsync()
        {
            LoadCount++;
            return Task.FromResult(CrtEffectsSettings.Default);
        }

        public Task<CrtEffectsSettings> ApplyAsync(CrtEffectsSettings settings) => Task.FromResult(settings);
    }

    private sealed class FakeShutdown : IAppShutdownService
    {
        public bool Available { get; set; }

        public int Shutdowns { get; private set; }

        public Task<bool> IsAvailableAsync() => Task.FromResult(Available);

        public Task ShutdownAsync()
        {
            Shutdowns++;
            return Task.CompletedTask;
        }
    }

    private sealed class UnusedMergeService : IRulesDataMergeService
    {
        public Task<InAppMergeResult> MergeAsync(IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedDownloads : IFileDownloadService
    {
        public Task DownloadTextAsync(string fileName, string content, string mimeType) => throw new NotSupportedException();
    }

    private sealed class UnusedUpdateService : IDataUpdateService
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DataUpdateResult> UpdateAsync(IProgress<DataUpdateProgress>? progress = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private readonly FakeEffects _effects = new();
    private readonly FakeShutdown _shutdown = new();

    public SettingsMenuTests()
    {
        Services.AddSingleton<ICrtEffectsService>(_effects);
        Services.AddSingleton<IRulesDataMergeService>(new UnusedMergeService());
        Services.AddSingleton<IAppShutdownService>(_shutdown);
        Services.AddSingleton<IFileDownloadService>(new UnusedDownloads());
        Services.AddSingleton<IDataUpdateService>(new UnusedUpdateService());
    }

    [Fact]
    public void The_panel_is_hidden_until_the_gear_is_clicked_and_hides_again_on_a_second_click()
    {
        var cut = Render<SettingsMenu>();
        Assert.True(cut.Find(".settings-panel").HasAttribute("hidden"));

        cut.Find(".settings-toggle").Click();
        Assert.False(cut.Find(".settings-panel").HasAttribute("hidden"));

        cut.Find(".settings-toggle").Click();
        Assert.True(cut.Find(".settings-panel").HasAttribute("hidden"));
    }

    [Fact]
    public void The_panel_holds_the_screen_effects_and_a_single_update_button_even_while_closed()
    {
        var cut = Render<SettingsMenu>();

        Assert.Single(cut.FindAll(".settings-panel .crt-effects-control"));
        Assert.Empty(cut.FindAll(".settings-panel button.merge-data-button"));
        Assert.Single(cut.FindAll(".settings-panel button.update-data-button"));
        Assert.Equal(1, _effects.LoadCount);
    }

    [Fact]
    public void Without_servo_there_is_no_close_button()
    {
        var cut = Render<SettingsMenu>();

        Assert.Empty(cut.FindAll("button.close-app-button"));
    }

    [Fact]
    public async Task Inside_servo_the_close_button_stops_the_app()
    {
        _shutdown.Available = true;
        var cut = Render<SettingsMenu>();

        await cut.Find("button.close-app-button").ClickAsync();

        Assert.Equal(1, _shutdown.Shutdowns);
    }

    [Fact]
    public async Task After_the_close_request_the_user_is_told_that_servo_is_closing()
    {
        _shutdown.Available = true;
        var cut = Render<SettingsMenu>();

        await cut.Find("button.close-app-button").ClickAsync();

        Assert.Contains("Closing Servo", cut.Find(".close-app-message").TextContent);
        Assert.Empty(cut.FindAll("button.close-app-button"));
    }
}
