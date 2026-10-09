using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.BattleTracker.Opponent;
using Servitor.App.BattleTracker.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Scoring.History;
using Servitor.App.Scoring.MissionDeck;
using Servitor.App.Scoring.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.Scoring;
using Servitor.App.Tests.Scoring.History;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;
using Servitor.App.RulesData.Storage;
using Servitor.App.Tests.RulesData.Loading;

namespace Servitor.App.Tests.BattleTracker.Pages;

public class PlayTests : BunitContext
{
    private readonly InMemoryRulesDataStore _store = new();

    public PlayTests()
    {
        Services.AddSingleton<IRulesDataStore>(_store);
        Services.AddSingleton<IBattleProgressState, BattleProgressState>();
        Services.AddSingleton<IArmyRuleState, ArmyRuleState>();
        Services.AddSingleton<IStratagemUsageState, StratagemUsageState>();
        Services.AddSingleton<IOpponentArmyState>(sp => new OpponentArmyState(
            sp.GetRequiredService<IMergedRulesDataProvider>(), new FakeDatasheetCatalog([])));
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

    [Fact]
    public void WithoutAnArmyThePromptReplacesTheTracker()
    {
        Services.AddSingleton<IArmyBuilderState>(new ArmyBuilderState());

        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        Assert.Contains("No army loaded", cut.Find(".play-empty").TextContent);
        Assert.Equal("", cut.Find(".import-link").GetAttribute("href"));
        Assert.Equal("build", cut.Find(".build-link").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".battle-progress"));
        Assert.Empty(cut.FindAll(".action-queue"));
    }

    [Fact]
    public void TheTrackerAppearsOnceAnArmyIsLoaded()
    {
        var army = new ArmyBuilderState();
        Services.AddSingleton<IArmyBuilderState>(army);
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        Assert.Empty(cut.FindAll(".battle-progress"));

        army.SelectBattleSize(BattleSizes.StrikeForce);
        army.SelectFaction("orks");
        army.AddUnit(Units.Sheet("Boyz", factions: ["orks"]), new UnitSize(10, 90));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".battle-progress")));
        Assert.Empty(cut.FindAll(".play-empty"));
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
        cut.Find(".use-stratagem").Click();

        Assert.Equal("0", cut.Find(".cp-you .cp-value").TextContent.Trim());
        Assert.Contains("Used this phase", cut.Find(".stratagem-card").TextContent);
    }

    [Fact]
    public void EnteringTheCommandPhaseCreditsBothPlayers()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();

        cut.Find(".phase-next").Click();

        Assert.Equal("1", cut.Find(".cp-you .cp-value").TextContent.Trim());
        Assert.Equal("1", cut.Find(".cp-enemy .cp-value").TextContent.Trim());
    }

    private static string[] RuleTitles(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut) =>
        cut.FindAll(".army-rules .rule-card h4").Select(h => h.TextContent.Trim()).ToArray();

    [Fact]
    public void ArmyRulesFollowThePhaseAndTheAurasToggleHidesAuras()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".army-rules")));
        Assert.Contains("No army rules for this phase.", cut.Find(".army-rules").TextContent);

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();

        Assert.Equal(["Mob Rule", "Banner (Aura)", "Tough"], RuleTitles(cut));

        cut.Find(".aura-toggle input").Change(false);

        Assert.Equal(["Mob Rule", "Tough"], RuleTitles(cut));
    }

    [Fact]
    public void PinningARuleKeepsItInEveryPhaseWithAnEditableNote()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".army-rules")));
        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();
        cut.Find(".army-rules .rule-card .pin-rule").Click();

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Movement").Click();

        Assert.Equal(["Mob Rule"], cut.FindAll(".army-rules > .rule-card h4").Select(h => h.TextContent.Trim()).ToArray());
        cut.Find(".pin-note").Change("Enemy Boyz");
        Assert.Equal("Enemy Boyz", cut.Find(".pin-note").GetAttribute("value"));

        cut.Find(".army-rules .rule-card .pin-rule").Click();
        Assert.Empty(cut.FindAll(".army-rules > .rule-card"));
    }

    [Fact]
    public void ARuleWithAlternativesOffersThemAsItsOwnEntryShapeAndTracksTheActiveOne()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".army-rules")));
        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Command").Click();

        Assert.Contains("Choose one", cut.Find(".army-rules .tag-choice").TextContent);
        Assert.Equal(2, cut.FindAll(".rule-option").Count);

        cut.FindAll(".activate-option")[0].Click();
        Assert.Contains("active", cut.FindAll(".rule-option")[0].ClassList);

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Fight").Click();
        Assert.Single(cut.FindAll(".army-rules > .rule-card"));

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Command").Click();
        cut.Find(".end-option").Click();
        Assert.Contains("used", cut.FindAll(".rule-option")[0].ClassList);
    }

    private void GoToShootingWithCp(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut, int cp)
    {
        for (var i = 0; i < cp; i++)
        {
            cut.Find(".cp-you .cp-plus").Click();
        }

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();
    }

    private static AngleSharp.Dom.IElement Card(IRenderedComponent<Servitor.App.BattleTracker.Pages.Play> cut, string name) =>
        cut.FindAll(".stratagem-card").First(c => c.QuerySelector("h3")!.TextContent.Trim() == name);

    [Fact]
    public void ASecondUseOfTheSameStratagemInAPhaseIsBlockedUntilTheNextPhase()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".stratagem-card")));
        GoToShootingWithCp(cut, 3);

        Card(cut, "GRENADE").QuerySelector("button.use-stratagem")!.Click();

        var used = Card(cut, "GRENADE");
        Assert.Contains("Used this phase", used.TextContent);
        Assert.Null(used.QuerySelector(".use-stratagem"));
        Assert.Equal("2", cut.Find(".cp-you .cp-value").TextContent.Trim());

        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Charge").Click();
        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();
        Assert.Contains("Used this phase", Card(cut, "GRENADE").TextContent);
    }

    [Fact]
    public void TargetingAnAlreadyTargetedUnitIsBlockedWithAReasonAndCanBeOverridden()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".stratagem-card")));
        GoToShootingWithCp(cut, 3);
        var unitId = cut.Find("select.use-target option:nth-child(2)").GetAttribute("value")!;

        Card(cut, "GRENADE").QuerySelector("select.use-target")!.Change(unitId);
        Card(cut, "GRENADE").QuerySelector("button.use-stratagem")!.Click();
        Card(cut, "BIG KRUNCH").QuerySelector("select.use-target")!.Change(unitId);
        Card(cut, "BIG KRUNCH").QuerySelector("button.use-stratagem")!.Click();

        Assert.Contains("already targeted by GRENADE", Card(cut, "BIG KRUNCH").QuerySelector(".use-blocked")!.TextContent);
        Assert.Equal("2", cut.Find(".cp-you .cp-value").TextContent.Trim());

        Card(cut, "BIG KRUNCH").QuerySelector("button.use-anyway")!.Click();

        Assert.Contains("Used this phase", Card(cut, "BIG KRUNCH").TextContent);
        Assert.Equal("1", cut.Find(".cp-you .cp-value").TextContent.Trim());
    }

    [Fact]
    public void UndoTakesAUseBackAndRefundsItsCost()
    {
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".stratagem-card")));
        GoToShootingWithCp(cut, 1);
        Card(cut, "GRENADE").QuerySelector("button.use-stratagem")!.Click();
        Assert.Equal("0", cut.Find(".cp-you .cp-value").TextContent.Trim());

        Card(cut, "GRENADE").QuerySelector("button.undo-stratagem")!.Click();

        Assert.Equal("1", cut.Find(".cp-you .cp-value").TextContent.Trim());
        Assert.NotNull(Card(cut, "GRENADE").QuerySelector("button.use-stratagem"));
    }

    private void AddScoringServices(BattleProgressState progress)
    {
        var deck = ScoringFixtures.Provider();
        var setup = new BattleSetupState(deck);
        var tracking = new ScoreTrackingState(setup, deck, progress);
        var scorecards = new FakeScorecardStore();
        Services.AddSingleton<IBattleSetupState>(setup);
        Services.AddSingleton<IMissionDeckProvider>(deck);
        Services.AddSingleton<IScoreTrackingState>(tracking);
        Services.AddSingleton<IScorecardStore>(scorecards);
        Services.AddSingleton<IGameRecorder>(new GameRecorder(setup, tracking, progress, new FakeBattlePlaySession(), scorecards, TimeProvider.System));
        setup.SelectYourDisposition(MissionArchetype.TakeAndHold);
        setup.SelectOpponentDisposition(MissionArchetype.PurgeTheFoe);
        setup.SelectRole(BattleRole.Attacker);
    }

    [Fact]
    public void TheScoringShortcutOpensTheSameScoringOverPlayAndSharesTheBattleRound()
    {
        var progress = new BattleProgressState(_store);
        Services.AddSingleton<IBattleProgressState>(progress);
        AddScoringServices(progress);
        var cut = Render<Servitor.App.BattleTracker.Pages.Play>();
        cut.Find(".round-next").Click();
        cut.FindAll(".phase-chip").First(c => c.TextContent.Trim() == "Shooting").Click();

        cut.Find(".open-scoring").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".score-tracker")));
        Assert.Contains("selected", cut.FindAll(".round-chip")[1].ClassList);
        Assert.Contains("Battle Round 2", cut.Find(".round-label").TextContent);

        cut.FindAll(".round-chip")[3].Click();
        Assert.Contains("Battle Round 4", cut.Find(".round-label").TextContent);

        cut.Find(".round-next").Click();
        Assert.Contains("selected", cut.FindAll(".round-chip")[4].ClassList);

        cut.Find(".modal-close").Click();
        Assert.Empty(cut.FindAll(".score-tracker"));
        Assert.Equal("Shooting", SelectedPhase(cut));
        Assert.Contains("Battle Round 5", cut.Find(".round-label").TextContent);
    }
}
