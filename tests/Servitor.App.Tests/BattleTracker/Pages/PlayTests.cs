using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Storage;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;

namespace Servitor.App.Tests.BattleTracker.Pages;

public class PlayTests : BunitContext
{
    private readonly InMemoryRulesDataStore _store = new();

    public PlayTests()
    {
        Services.AddSingleton<IRulesDataStore>(_store);
        Services.AddSingleton<IBattleProgressState, BattleProgressState>();
    }

    private static string SelectedPhase(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut) =>
        cut.Find(".phase-chip.selected").TextContent.Trim();

    [Fact]
    public void ShowsRoundOneAndDeploymentInitially()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        Assert.Equal("Deployment", SelectedPhase(cut));
        Assert.Contains("Battle Round 1", cut.Find(".round-label").TextContent);
    }

    [Fact]
    public void NextAndPreviousButtonsMoveThePhase()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.Find(".phase-next").Click();
        cut.Find(".phase-next").Click();
        Assert.Equal("Movement", SelectedPhase(cut));

        cut.Find(".phase-previous").Click();
        Assert.Equal("Command", SelectedPhase(cut));
    }

    [Fact]
    public void ClickingAPhaseChipJumpsThereAndDeploymentCanBeReentered()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.Find(".phase-next").Click();

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Deployment").Click();

        Assert.Equal("Deployment", SelectedPhase(cut));
        Assert.True(cut.Find(".phase-previous").HasAttribute("disabled"));
    }

    [Fact]
    public void RoundButtonsRespectTheBounds()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        Assert.True(cut.Find(".round-previous").HasAttribute("disabled"));

        for (var i = 0; i < BattleProgressState.MaxRound - 1; i++)
        {
            cut.Find(".round-next").Click();
        }

        Assert.Contains($"Battle Round {BattleProgressState.MaxRound}", cut.Find(".round-label").TextContent);
        Assert.True(cut.Find(".round-next").HasAttribute("disabled"));
    }

    [Fact]
    public void RestoresPersistedProgressWhenOpened()
    {
        _store.Values["battle/progress"] = new BattleProgress(4, BattlePhase.Fight, Side.You, Side.Enemy);

        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.WaitForAssertion(() => Assert.Equal("Fight", SelectedPhase(cut)));
        Assert.Contains("Battle Round 4", cut.Find(".round-label").TextContent);
        Assert.Equal("Enemy turn", cut.Find(".turn-indicator").TextContent.Trim());
    }

    [Fact]
    public void TheTurnIndicatorSwitchesAfterEndOfTurnAndTheRoundCountsAfterBothTurns()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        Assert.Equal("Your turn", cut.Find(".turn-indicator").TextContent.Trim());

        for (var i = 0; i < 7; i++)
        {
            cut.Find(".phase-next").Click();
        }

        Assert.Equal("Enemy turn", cut.Find(".turn-indicator").TextContent.Trim());
        Assert.Contains("Battle Round 1", cut.Find(".round-label").TextContent);

        for (var i = 0; i < 6; i++)
        {
            cut.Find(".phase-next").Click();
        }

        Assert.Equal("Your turn", cut.Find(".turn-indicator").TextContent.Trim());
        Assert.Contains("Battle Round 2", cut.Find(".round-label").TextContent);
    }

    [Fact]
    public void FirstTurnCanBeChosenInDeploymentAndThePickerDisappearsAfterwards()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.Find(".first-turn-enemy").Click();
        Assert.Equal("Enemy turn", cut.Find(".turn-indicator").TextContent.Trim());

        cut.Find(".phase-next").Click();
        Assert.Empty(cut.FindAll(".first-turn-picker"));
    }
}
