using System.Text.Json;
using Servitor.App.Scoring.MissionDeck;
using Servitor.DataUpdate.MissionDeck;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.DataUpdate;

public sealed class MissionDeckScraperTests
{
    private static readonly DateOnly ReadOn = new(2026, 10, 1);

    private static MissionDeckScrapeResult ScrapePrimary(string body, string intro = "") =>
        Scrape(SampleMissionDeckPage.PrimaryCard("PURGE AND SECURE", "TakeAndHold", "Reconnaissance", body, intro), "");

    private static MissionDeckScrapeResult Scrape(string primaryCards, string secondaryCards) =>
        MissionDeckScraper.Scrape(
            "<h2 id=\"Primary-Mission-deck\">x</h2>" + primaryCards
            + "<h2 id=\"Deployment-deck\">x</h2><h2 id=\"Secondary-Mission-deck\">x</h2>" + secondaryCards + "<h2 id=\"Twist-deck\">x</h2>",
            ReadOn);

    private static MissionDeckScrapeResult ScrapeSecondary(string body, string intro = "", bool fixedEligible = false, string tail = "") =>
        Scrape("", SampleMissionDeckPage.SecondaryCard("A TEMPTING TARGET", fixedEligible, body, intro, tail));

    private static string Line(string text, string vp, string prefix = "") =>
        $"<div class=\"caPmScore\">{prefix}<span>{text}</span>{vp}</div>";

    private static JsonElement Primary(MissionDeckScrapeResult result) =>
        JsonDocument.Parse(result.Json).RootElement.GetProperty("primaryMissions")[0];

    private static JsonElement Secondary(MissionDeckScrapeResult result) =>
        JsonDocument.Parse(result.Json).RootElement.GetProperty("secondaryMissions")[0];

    [Fact]
    public void ReadsNameLegendAndBothDispositionsOfAPrimaryMission()
    {
        var mission = Primary(ScrapePrimary("<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("You hold things.", "<span class=\"caPmVP\">2VP</span>")));

        Assert.Equal("Purge and Secure", mission.GetProperty("name").GetString());
        Assert.Equal("Some legend text.", mission.GetProperty("legend").GetString());
        Assert.Equal("TakeAndHold", mission.GetProperty("yourDisposition").GetString());
        Assert.Equal("Reconnaissance", mission.GetProperty("opponentDisposition").GetString());
    }

    [Theory]
    [InlineData("ANY BATTLE ROUND", 1, 5)]
    [InlineData("SECOND BATTLE ROUND ONWARDS", 2, 5)]
    [InlineData("FOURTH BATTLE ROUND ONWARDS", 4, 5)]
    [InlineData("SECOND TO FOURTH BATTLE ROUND", 2, 4)]
    [InlineData("FIRST AND SECOND BATTLE ROUND", 1, 2)]
    [InlineData("FIFTH BATTLE ROUND", 5, 5)]
    [InlineData("FIRST BATTLE ROUND", 1, 1)]
    public void MapsTheRoundHeadingToTheRoundsTheLineCanScoreIn(string heading, int first, int last)
    {
        var line = Primary(ScrapePrimary($"<div class=\"caPmHdr\">{heading}</div>" + Line("You hold things.", "<span class=\"caPmVP\">2VP</span>")))
            .GetProperty("scoringConditions")[0];

        Assert.Equal(first, line.GetProperty("firstRound").GetInt32());
        Assert.Equal(last, line.GetProperty("lastRound").GetInt32());
    }

    [Fact]
    public void ReadsTheScopeTimingConditionAndPointsOfALine()
    {
        var line = Primary(ScrapePrimary(
                "<div class=\"caPmHdr\">SECOND BATTLE ROUND ONWARDS</div><div class=\"caPmWhen\"><b>WHEN:</b> End of your <a href=\"/x\">Command phase</a>.</div>"
                + Line("For each <b>objective</b> you control.", "<span class=\"caPmVP\">3VP</span>")))
            .GetProperty("scoringConditions")[0];

        Assert.Equal("Second Battle Round Onwards", line.GetProperty("roundScope").GetString());
        Assert.Equal("End of your Command phase.", line.GetProperty("when").GetString());
        Assert.Equal("For each objective you control.", line.GetProperty("condition").GetString());
        Assert.Equal(3, line.GetProperty("points").GetInt32());
        Assert.False(line.TryGetProperty("relation", out _));
    }

    [Fact]
    public void ReadsOrAndCumulativeLines()
    {
        var lines = Primary(ScrapePrimary(
                "<div class=\"caPmHdr\">ANY BATTLE ROUND</div><div class=\"caPmWhen\"><b>WHEN:</b> End of your turn.</div>"
                + Line("First.", "<span class=\"caPmVP\">3VP</span>")
                + Line("Second.", "<span class=\"caPmVP\">3VP</span>", "<span class=\"caPmOr\">OR</span>")
                + Line("Third.", "<span class=\"caPmVPCol\"><span class=\"caPmVP\">+2VP</span><span class=\"caPmCumul\">CUMULATIVE</span></span>", "<span class=\"caPmPlus\">+</span>")))
            .GetProperty("scoringConditions");

        Assert.False(lines[0].TryGetProperty("relation", out _));
        Assert.Equal("Or", lines[1].GetProperty("relation").GetString());
        Assert.Equal("Cumulative", lines[2].GetProperty("relation").GetString());
        Assert.Equal(2, lines[2].GetProperty("points").GetInt32());
    }

    [Fact]
    public void AnEndOfBattleLineHasNoTimingAndScoresInTheLastRound()
    {
        var line = Primary(ScrapePrimary("<div class=\"caPmHdr\">END OF THE BATTLE</div>" + Line("You hold the prize.", "<span class=\"caPmVP\">5VP</span>")))
            .GetProperty("scoringConditions")[0];

        Assert.Equal("", line.GetProperty("when").GetString());
        Assert.True(line.GetProperty("endOfBattle").GetBoolean());
        Assert.Equal(5, line.GetProperty("firstRound").GetInt32());
    }

    [Fact]
    public void KeepsTheLabelledIntroAndTheActionsOfAPrimaryMissionAsAdditionalRules()
    {
        var result = ScrapePrimary(
            "<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("You hold things.", "<span class=\"caPmVP\">2VP</span>"),
            "<div class=\"caPmIntro\"><span class=\"caActLbl\">START OF YOUR TURN:</span> Pick <b>one</b> unit.</div>");

        Assert.Equal("START OF YOUR TURN: Pick one unit.", Primary(result).GetProperty("additionalRules")[0].GetString());
    }

    [Fact]
    public void ALineWithoutAModeLabelScoresTheSameInBothSecondaryModes()
    {
        var condition = Secondary(ScrapeSecondary("<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("You hold it.", "<span class=\"caPmVP\">5VP</span>")))
            .GetProperty("conditions")[0];

        Assert.Equal(5, condition.GetProperty("fixedPoints").GetInt32());
        Assert.Equal(5, condition.GetProperty("tacticalPoints").GetInt32());
    }

    [Fact]
    public void ReadsFixedOnlyTacticalOnlyAndPairedLines()
    {
        var conditions = Secondary(ScrapeSecondary(
                "<div class=\"caPmHdr\">ANY BATTLE ROUND</div>"
                + Line("Fixed only.", "<span class=\"caPmVPCol\"><span class=\"caPmVP\">3VP</span><span class=\"caPmCumul\">FIXED</span></span>")
                + Line("Tactical only.", "<span class=\"caPmVPCol\"><span class=\"caPmVP\">5VP</span><span class=\"caPmCumul\">TACTICAL</span></span>")
                + Line("Both.", "<span class=\"caPmVPPair\"><span class=\"caPmVPCol\"><span class=\"caPmVP\">4VP</span><span class=\"caPmCumul\">FIXED</span></span><span class=\"caPmVPCol\"><span class=\"caPmVP\">5VP</span><span class=\"caPmCumul\">TACTICAL</span></span></span>"),
                fixedEligible: true))
            .GetProperty("conditions");

        Assert.Equal(3, conditions[0].GetProperty("fixedPoints").GetInt32());
        Assert.False(conditions[0].TryGetProperty("tacticalPoints", out _));
        Assert.False(conditions[1].TryGetProperty("fixedPoints", out _));
        Assert.Equal(5, conditions[1].GetProperty("tacticalPoints").GetInt32());
        Assert.Equal(4, conditions[2].GetProperty("fixedPoints").GetInt32());
        Assert.Equal(5, conditions[2].GetProperty("tacticalPoints").GetInt32());
    }

    [Fact]
    public void ReadsTheFixedMarkerAndTheTurnCap()
    {
        var mission = Secondary(ScrapeSecondary(
            "<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("Each.", "<span class=\"caPmVPCol\"><span class=\"caPmVP\">2VP</span><span class=\"caPmCumul\">(UP TO 5VP)</span></span>"),
            fixedEligible: true));

        Assert.True(mission.GetProperty("fixedEligible").GetBoolean());
        Assert.Equal(5, mission.GetProperty("turnCap").GetInt32());
    }

    [Theory]
    [InlineData("WHEN DRAWN:", "Pick a unit.")]
    [InlineData("WHEN DRAWN/START OF YOUR TURN:", "(WHEN DRAWN/START OF YOUR TURN) Pick a unit.")]
    public void ReadsWhenDrawnFromTheIntro(string label, string expected)
    {
        var mission = Secondary(ScrapeSecondary(
            "<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("Each.", "<span class=\"caPmVP\">2VP</span>"),
            $"<div class=\"caPmIntro\"><span class=\"caActLbl\">{label}</span> Pick a unit.</div>"));

        Assert.Equal(expected, mission.GetProperty("whenDrawn").GetString());
    }

    [Fact]
    public void WritesAnObjectiveActionAsOneAdditionalRule()
    {
        const string action = "<div class=\"caAct\"><div class=\"caActHdr\">PLUNDER<span class=\"caActIco\"></span></div><div class=\"caActType\">OBJECTIVE ACTION</div>"
            + "<div class=\"caActBody\"><div class=\"caActRow\"><span class=\"caActLbl\">STARTS:</span> Your <a href=\"/x\">Shooting phase</a>.</div>"
            + "<div class=\"caActRow\"><span class=\"caActLbl\">EFFECT:</span> That area is <b>plundered</b>.</div></div></div>";

        var mission = Secondary(ScrapeSecondary("<div class=\"caPmHdr\">ANY BATTLE ROUND</div>" + Line("Each.", "<span class=\"caPmVP\">2VP</span>"), intro: action));

        Assert.Equal("PLUNDER — STARTS: Your Shooting phase. EFFECT: That area is plundered.", mission.GetProperty("additionalRules")[0].GetString());
    }

    [Fact]
    public void AMissionWithoutAScoringLineIsLeftOutAndReported()
    {
        var result = ScrapeSecondary("");

        Assert.Equal(0, result.SecondaryMissions);
        Assert.Contains(result.Warnings, w => w.Contains("no scoring line", StringComparison.Ordinal));
    }

    [Fact]
    public void DeploymentCardsGetTheirKnownDescriptionAndUnknownOnesAreReported()
    {
        var result = MissionDeckScraper.Scrape(SampleMissionDeckPage.Page(), ReadOn);
        var cards = JsonDocument.Parse(result.Json).RootElement.GetProperty("deploymentCards");

        Assert.Equal(5, result.DeploymentCards);
        Assert.Equal("Hammer and Anvil", cards[0].GetProperty("name").GetString());
        Assert.Contains("top (short) battlefield edge", cards[0].GetProperty("attackerZone").GetString());
        Assert.Equal("", cards[4].GetProperty("attackerZone").GetString());
        Assert.Contains(result.Warnings, w => w.Contains("Unknown Layout", StringComparison.Ordinal));
    }

    [Fact]
    public void ACardWithoutForceDispositionsIsLeftOut()
    {
        var result = Scrape("<div class=\"cgCard_pad\"><div class=\"ca7Name\">BROKEN</div></div>", "");

        Assert.Equal(0, result.PrimaryMissions);
        Assert.Contains(result.Warnings, w => w.Contains("force dispositions", StringComparison.Ordinal));
    }

    [Fact]
    public void ASectionTheLayoutNoLongerHasYieldsNoCards()
    {
        var result = MissionDeckScraper.Scrape("<html><h1>Something else</h1></html>", ReadOn);

        Assert.Equal((0, 0, 0), (result.PrimaryMissions, result.DeploymentCards, result.SecondaryMissions));
    }

    [Fact]
    public void TheScrapedFileIsReadByTheAppsMissionDeckParser()
    {
        var result = MissionDeckScraper.Scrape(SampleMissionDeckPage.Page(), ReadOn);

        var deck = MissionDeckParser.Parse(result.Json);

        Assert.Empty(deck.Warnings);
        Assert.Equal(5, deck.Data.DeploymentCards.Count);
        Assert.Equal(10, deck.Data.SecondaryMissions.Count);
        Assert.NotNull(deck.Data.Resolve(MissionArchetype.TakeAndHold, MissionArchetype.Disruption));
    }
}
