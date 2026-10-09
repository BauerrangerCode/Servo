using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Loading;
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
        Services.AddSingleton<IArmyBuilderState>(BattleTrackerFixtures.OrkArmy());
        Services.AddSingleton<IMergedRulesDataProvider>(new BattleTrackerFixtures.FakeRulesDataProvider(BattleTrackerFixtures.Data()));
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

    private static string[] CardNames(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut) =>
        cut.FindAll(".stratagem-card h3").Select(h => h.TextContent.Trim()).ToArray();

    [Fact]
    public void TheQueueFollowsThePhaseAndShowsTheCardDetails()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.Equal(["COMMAND RE-ROLL"], CardNames(cut)));

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();

        Assert.Equal(["COMMAND RE-ROLL", "GRENADE", "BIG KRUNCH"], CardNames(cut));
        var card = cut.FindAll(".stratagem-card").Last();
        Assert.Contains("1 CP", card.QuerySelector(".stratagem-cost")!.TextContent);
        Assert.Contains("Battle Tactic", card.QuerySelector(".tag-category")!.TextContent);
        Assert.Contains("Your Shooting or Charge phase", card.QuerySelector(".tag-timing")!.TextContent);
        Assert.Contains("When text.", card.QuerySelector(".section-when")!.TextContent);
        Assert.Contains("Target text.", card.QuerySelector(".section-target")!.TextContent);
        Assert.Contains("Effect text.", card.QuerySelector(".section-effect")!.TextContent);
        Assert.Null(card.QuerySelector(".section-restrictions"));
    }

    [Fact]
    public void UsingAStratagemSpendsCommandPointsAndIsRefusedWhenTheyAreMissing()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".stratagem-card")));
        Assert.True(cut.Find(".use-stratagem").HasAttribute("disabled"));

        cut.Find(".cp-you .cp-plus").Click();
        cut.Find(".cp-you .cp-plus").Click();
        cut.Find(".use-stratagem").Click();

        Assert.Equal("1", cut.Find(".cp-you .cp-value").TextContent.Trim());
        cut.Find(".use-stratagem").Click();
        Assert.Equal("0", cut.Find(".cp-you .cp-value").TextContent.Trim());
        Assert.True(cut.Find(".use-stratagem").HasAttribute("disabled"));
    }

    [Fact]
    public void EnteringTheCommandPhaseCreditsBothPlayers()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.Find(".phase-next").Click();

        Assert.Equal("1", cut.Find(".cp-you .cp-value").TextContent.Trim());
        Assert.Equal("1", cut.Find(".cp-enemy .cp-value").TextContent.Trim());
    }
}
