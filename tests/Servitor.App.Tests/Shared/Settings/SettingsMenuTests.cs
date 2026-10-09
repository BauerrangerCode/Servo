using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Merging;
using Servitor.App.RulesData.Updating;
using Servitor.App.Shared.Effects;
using Servitor.App.Shared.Files;
using Servitor.App.Shared.Settings;
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

    public SettingsMenuTests()
    {
        Services.AddSingleton<ICrtEffectsService>(_effects);
        Services.AddSingleton<IRulesDataMergeService>(new UnusedMergeService());
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
}
