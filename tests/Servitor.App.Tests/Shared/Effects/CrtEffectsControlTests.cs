using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.Shared.Effects;

namespace Servitor.App.Tests.Shared.Effects;

public class CrtEffectsControlTests : BunitContext
{
    private sealed class FakeEffects : ICrtEffectsService
    {
        public List<CrtEffectsSettings> Applied { get; } = [];

        public Task<CrtEffectsSettings> LoadAsync() => Task.FromResult(CrtEffectsSettings.Default);

        public Task<CrtEffectsSettings> ApplyAsync(CrtEffectsSettings settings)
        {
            var normalized = settings.Normalized();
            Applied.Add(normalized);
            return Task.FromResult(normalized);
        }
    }

    private (IRenderedComponent<CrtEffectsControl> Cut, FakeEffects Effects) RenderControl()
    {
        var effects = new FakeEffects();
        Services.AddSingleton<ICrtEffectsService>(effects);
        return (Render<CrtEffectsControl>(), effects);
    }

    [Fact]
    public void Shows_the_loaded_interval()
    {
        var (cut, _) = RenderControl();

        Assert.Equal("0.5", cut.Find(".fx-min").GetAttribute("value"));
        Assert.Equal("1.5", cut.Find(".fx-max").GetAttribute("value"));
    }

    [Fact]
    public void Changing_the_interval_applies_it_and_keeps_max_at_or_above_min()
    {
        var (cut, effects) = RenderControl();

        cut.Find(".fx-min").Change("5");

        Assert.Equal(new CrtEffectsSettings(true, 5, 5), effects.Applied[^1]);
        Assert.Equal("5", cut.Find(".fx-max").GetAttribute("value"));
    }

    [Fact]
    public void Disabling_the_effects_applies_it_and_disables_the_interval_inputs()
    {
        var (cut, effects) = RenderControl();

        cut.Find(".fx-enabled").Change(false);

        Assert.False(effects.Applied[^1].Enabled);
        Assert.True(cut.Find(".fx-min").HasAttribute("disabled"));
    }
}
