using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Servitor.DataUpdate.CoreRules;

namespace Servitor.DataUpdate.MissionDeck;

/// <summary>The scraped Mission Deck as the JSON text <c>missions/mission-deck.json</c> holds, with what was counted and what looked wrong.</summary>
public sealed record MissionDeckScrapeResult(string Json, int PrimaryMissions, int DeploymentCards, int SecondaryMissions, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads the Wahapedia Mission Deck page (https://wahapedia.ru/wh40k11ed/the-rules/mission-deck-2026-27/) into the
/// file the Scoring screen loads (see <c>MissionDeckParser</c> in the app for its format). The page renders every
/// card as a <c>cgCard_pad</c> block; the Primary Mission, Deployment and Secondary Mission decks are told apart by
/// their headings, the Twist deck is ignored. A card is read from its CSS classes: <c>caPmHdr</c> names the battle
/// rounds of the scoring lines that follow, <c>caPmWhen</c> their timing, <c>caPmScore</c> one line with its VP.
/// </summary>
public static partial class MissionDeckScraper
{
    private const int FirstRound = 1;
    private const int LastRound = 5;

    private static readonly string[] Ordinals = ["first", "second", "third", "fourth", "fifth"];
    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase) { "and", "of", "on", "the", "to", "in", "for", "at", "by" };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static MissionDeckScrapeResult Scrape(string pageHtml, DateOnly readOn)
    {
        var warnings = new List<string>();
        var primary = Section(pageHtml, "Primary-Mission-deck", "Deployment-deck");
        var deployment = Section(pageHtml, "Deployment-deck", "Secondary-Mission-deck");
        var secondary = Section(pageHtml, "Secondary-Mission-deck", "Twist-deck");

        var primaryMissions = Cards(primary).Select(card => ReadPrimaryMission(card, warnings)).OfType<JsonObject>().ToList();
        var deploymentCards = Cards(deployment).Select(card => ReadDeploymentCard(card, warnings)).OfType<JsonObject>().ToList();
        var secondaryMissions = Cards(secondary).Select(card => ReadSecondaryMission(card, warnings)).OfType<JsonObject>().ToList();

        var root = new JsonObject
        {
            ["_source"] = $"wahapedia.ru/wh40k11ed/the-rules/mission-deck-2026-27/ (Chapter Approved 2026-27: Mission Deck), read {readOn:yyyy-MM-dd}. Twist deck is out of scope.",
            ["primaryMissions"] = new JsonArray(primaryMissions.Select(m => (JsonNode?)m).ToArray()),
            ["deploymentCards"] = new JsonArray(deploymentCards.Select(c => (JsonNode?)c).ToArray()),
            ["secondaryMissions"] = new JsonArray(secondaryMissions.Select(m => (JsonNode?)m).ToArray()),
        };

        return new MissionDeckScrapeResult(root.ToJsonString(WriteOptions), primaryMissions.Count, deploymentCards.Count, secondaryMissions.Count, warnings);
    }

    // ---- page structure ----------------------------------------------------------------------------------------

    // The part of the page between two headings; empty when the page has no such heading (the layout changed).
    private static string Section(string pageHtml, string headingId, string nextHeadingId)
    {
        var start = pageHtml.IndexOf($"id=\"{headingId}\"", StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        var end = pageHtml.IndexOf($"id=\"{nextHeadingId}\"", start, StringComparison.Ordinal);
        return end < 0 ? pageHtml[start..] : pageHtml[start..end];
    }

    private static IEnumerable<string> Cards(string section) => CardSplitRegex().Split(section).Skip(1);

    private static string Name(string card) => TitleCase(CoreRuleHtml.PlainText(CardNameRegex().Match(card).Groups["name"].Value));

    private static string Legend(string card) =>
        LegendRegex().Match(card) is { Success: true } match ? CoreRuleHtml.PlainText(match.Groups["text"].Value) : "";

    // ---- primary missions --------------------------------------------------------------------------------------

    private static JsonObject? ReadPrimaryMission(string card, List<string> warnings)
    {
        var name = Name(card);
        var dispositions = DispositionRegex().Matches(card);
        var yours = DispositionClassRegex().Match(card).Groups["name"].Value;
        if (yours.Length == 0 || dispositions.Count < 2)
        {
            warnings.Add($"Primary mission \"{name}\": the force dispositions were not found; the mission was left out.");
            return null;
        }

        var body = ReadBody(card, warnings, name);
        var mission = new JsonObject
        {
            ["yourDisposition"] = yours,
            ["opponentDisposition"] = dispositions[1].Groups["name"].Value,
            ["name"] = name,
            ["legend"] = Legend(card),
            ["scoringConditions"] = new JsonArray(body.Lines.Select(line => (JsonNode?)line.ToPrimaryJson()).ToArray()),
        };

        var rules = body.Intros.Select(i => i.Label.Length > 0 ? $"{i.Label} {i.Text}" : i.Text).Concat(body.Actions).ToList();
        if (rules.Count > 0)
        {
            mission["additionalRules"] = new JsonArray(rules.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray());
        }

        if (body.Lines.Count == 0)
        {
            warnings.Add($"Primary mission \"{name}\": no scoring line was found.");
        }

        return mission;
    }

    // ---- deployment cards --------------------------------------------------------------------------------------

    private static JsonObject ReadDeploymentCard(string card, List<string> warnings)
    {
        var name = Name(card);
        var description = DeploymentCardDescriptions.Find(name);
        if (description is null)
        {
            warnings.Add($"Deployment card \"{name}\" has no description yet (the page shows it as a picture only); add one to DeploymentCardDescriptions.");
        }

        return new JsonObject
        {
            ["name"] = name,
            ["attackerZone"] = description?.AttackerZone ?? "",
            ["defenderZone"] = description?.DefenderZone ?? "",
            ["objectives"] = description?.Objectives ?? "",
        };
    }

    // ---- secondary missions ------------------------------------------------------------------------------------

    private static JsonObject? ReadSecondaryMission(string card, List<string> warnings)
    {
        var name = Name(card);
        var body = ReadBody(card, warnings, name);
        if (body.Lines.Count == 0)
        {
            warnings.Add($"Secondary mission \"{name}\": no scoring line was found; the mission was left out.");
            return null;
        }

        var mission = new JsonObject { ["name"] = name, ["legend"] = Legend(card) };

        var drawn = body.Intros.FirstOrDefault(i => i.Label.Contains("WHEN DRAWN", StringComparison.Ordinal));
        if (drawn is not null)
        {
            // "WHEN DRAWN:" is the plain case; a combined label ("WHEN DRAWN/START OF YOUR TURN:") stays part of the text.
            mission["whenDrawn"] = drawn.Label == "WHEN DRAWN:" ? drawn.Text : $"({drawn.Label.TrimEnd(':')}) {drawn.Text}";
        }

        var rules = body.Intros.Where(i => i != drawn).Select(i => i.Label.Length > 0 ? $"{i.Label} {i.Text}" : i.Text)
            .Concat(body.Actions).Concat(body.Notes).ToList();
        if (rules.Count > 0)
        {
            mission["additionalRules"] = new JsonArray(rules.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray());
        }

        if (FixedMarkerRegex().IsMatch(card))
        {
            mission["fixedEligible"] = true;
        }

        if (body.TurnCap is { } cap)
        {
            mission["turnCap"] = cap;
        }

        mission["conditions"] = new JsonArray(body.Lines.Select(line => (JsonNode?)line.ToSecondaryJson()).ToArray());
        return mission;
    }

    // ---- shared card body --------------------------------------------------------------------------------------

    private sealed record Intro(string Label, string Text);

    private sealed record ScoreLine(string RoundScope, string When, string Condition, int FirstRound, int LastRound, bool EndOfBattle, string Relation, int? FixedPoints, int? TacticalPoints, int? Points)
    {
        public JsonObject ToPrimaryJson()
        {
            var json = new JsonObject
            {
                ["roundScope"] = RoundScope,
                ["when"] = When,
                ["condition"] = Condition,
                ["points"] = Points ?? FixedPoints ?? TacticalPoints ?? 0,
            };
            if (Relation.Length > 0)
            {
                json["relation"] = Relation;
            }

            json["firstRound"] = FirstRound;
            json["lastRound"] = LastRound;
            if (EndOfBattle)
            {
                json["endOfBattle"] = true;
            }

            return json;
        }

        public JsonObject ToSecondaryJson()
        {
            var json = new JsonObject
            {
                ["roundScope"] = RoundScope,
                ["when"] = When,
                ["condition"] = Condition,
                ["firstRound"] = FirstRound,
                ["lastRound"] = LastRound,
            };
            if (FixedPoints is { } fixedPoints)
            {
                json["fixedPoints"] = fixedPoints;
            }

            if (TacticalPoints is { } tacticalPoints)
            {
                json["tacticalPoints"] = tacticalPoints;
            }

            if (Relation.Length > 0)
            {
                json["relation"] = Relation;
            }

            return json;
        }
    }

    private sealed record CardBody(List<Intro> Intros, List<string> Actions, List<string> Notes, List<ScoreLine> Lines, int? TurnCap);

    // Walks the card's tagged blocks in page order; a header or timing applies to the score lines that follow it.
    private static CardBody ReadBody(string card, List<string> warnings, string cardName)
    {
        var body = new CardBody([], [], [], [], null);
        var roundScope = "";
        var (firstRound, lastRound, endOfBattle) = (FirstRound, LastRound, false);
        var when = "";
        var turnCap = (int?)null;

        foreach (Match block in BlockRegex().Matches(card))
        {
            if (block.Groups["introHtml"].Success)
            {
                body.Intros.Add(ReadIntro(block.Groups["introHtml"].Value));
            }
            else if (block.Groups["hdrHtml"].Success)
            {
                var header = CoreRuleHtml.PlainText(block.Groups["hdrHtml"].Value);
                roundScope = TitleCase(header);
                (firstRound, lastRound, endOfBattle) = ReadRounds(header, warnings, cardName);
                when = "";
            }
            else if (block.Groups["whenHtml"].Success)
            {
                when = CoreRuleHtml.PlainText(block.Groups["whenHtml"].Value);
            }
            else if (block.Groups["scoreHtml"].Success)
            {
                var line = ReadScoreLine(block.Groups["scoreHtml"].Value, roundScope, when, firstRound, lastRound, endOfBattle, ref turnCap, warnings, cardName);
                if (line is not null)
                {
                    body.Lines.Add(line);
                }
            }
            else if (block.Groups["noteHtml"].Success)
            {
                body.Notes.Add(CoreRuleHtml.PlainText(block.Groups["noteHtml"].Value));
            }
            else if (block.Groups["actName"].Success)
            {
                body.Actions.Add(ReadAction(block));
            }
        }

        return body with { TurnCap = turnCap };
    }

    private static Intro ReadIntro(string html)
    {
        var label = LabelRegex().Match(html);
        return label.Success
            ? new Intro(CoreRuleHtml.PlainText(label.Groups["label"].Value), CoreRuleHtml.PlainText(html[(label.Index + label.Length)..]))
            : new Intro("", CoreRuleHtml.PlainText(html));
    }

    // "BOOBY TRAP — STARTS: Your Shooting phase. UNITS: ..." (the action's name, then its rows).
    private static string ReadAction(Match block)
    {
        var name = CoreRuleHtml.PlainText(block.Groups["actName"].Value);
        var rows = ActionRowRegex().Matches(block.Groups["actRows"].Value).Select(row => CoreRuleHtml.PlainText(row.Groups["row"].Value));
        return $"{name} — {string.Join(' ', rows)}";
    }

    private static ScoreLine? ReadScoreLine(string html, string roundScope, string when, int firstRound, int lastRound, bool endOfBattle, ref int? turnCap, List<string> warnings, string cardName)
    {
        var vpStart = html.IndexOf("<span class=\"caPmVP", StringComparison.Ordinal);
        if (vpStart < 0)
        {
            warnings.Add($"\"{cardName}\": a scoring line has no VP value and was left out.");
            return null;
        }

        var textHtml = html[..vpStart];
        var relation = textHtml.Contains("class=\"caPmOr\"", StringComparison.Ordinal) ? "Or"
            : textHtml.Contains("class=\"caPmPlus\"", StringComparison.Ordinal) ? "Cumulative"
            : "";
        var condition = CoreRuleHtml.PlainText(MarkerRegex().Replace(textHtml, ""));

        int? fixedPoints = null;
        int? tacticalPoints = null;
        int? plainPoints = null;
        foreach (Match value in VictoryPointsRegex().Matches(html[vpStart..]))
        {
            var points = int.Parse(value.Groups["vp"].Value);
            var labels = LabelSpanRegex().Matches(value.Groups["labels"].Value).Select(l => l.Groups["label"].Value.Trim()).ToList();
            if (labels.Contains("CUMULATIVE") && relation.Length == 0)
            {
                relation = "Cumulative";
            }

            if (labels.FirstOrDefault(l => TurnCapRegex().IsMatch(l)) is { } capLabel)
            {
                turnCap = int.Parse(TurnCapRegex().Match(capLabel).Groups["cap"].Value);
            }

            if (labels.Contains("FIXED"))
            {
                fixedPoints = points;
            }
            else if (labels.Contains("TACTICAL"))
            {
                tacticalPoints = points;
            }
            else
            {
                plainPoints = points;
            }
        }

        // A line without a mode label scores the same in both modes.
        if (fixedPoints is null && tacticalPoints is null)
        {
            fixedPoints = tacticalPoints = plainPoints;
        }

        return new ScoreLine(roundScope, when, condition, firstRound, lastRound, endOfBattle, relation, fixedPoints, tacticalPoints, plainPoints);
    }

    // "SECOND BATTLE ROUND ONWARDS" → 2-5, "SECOND TO FOURTH BATTLE ROUND" → 2-4, "END OF THE BATTLE" → the last round.
    private static (int First, int Last, bool EndOfBattle) ReadRounds(string header, List<string> warnings, string cardName)
    {
        var text = header.ToLowerInvariant();
        if (text.StartsWith("end of the battle", StringComparison.Ordinal))
        {
            return (LastRound, LastRound, true);
        }

        if (text.StartsWith("any battle round", StringComparison.Ordinal))
        {
            return (FirstRound, LastRound, false);
        }

        var rounds = Ordinals.Select((word, index) => (Word: word, Round: index + 1))
            .Where(o => Regex.IsMatch(text, $@"\b{o.Word}\b"))
            .Select(o => o.Round)
            .ToList();
        if (rounds.Count == 0)
        {
            warnings.Add($"\"{cardName}\": the round heading \"{header}\" was not understood; 1-5 was assumed.");
            return (FirstRound, LastRound, false);
        }

        var first = rounds.Min();
        return text.Contains("onwards", StringComparison.Ordinal) ? (first, LastRound, false) : (first, rounds.Max(), false);
    }

    // "PURGE AND SECURE" → "Purge and Secure"; small words stay lower case except at the start, an apostrophe does not start a word.
    private static string TitleCase(string text) =>
        string.Join(' ', text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((word, index) => index > 0 && SmallWords.Contains(word) ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    // ---- patterns ----------------------------------------------------------------------------------------------

    [GeneratedRegex(@"<div class=""cgCard_pad"">")]
    private static partial Regex CardSplitRegex();

    [GeneratedRegex(@"<div class=""ca7Name"">(?<name>.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex CardNameRegex();

    [GeneratedRegex(@"<p class=""ShowFluff ca7Legend"">(?<text>.*?)</p>", RegexOptions.Singleline)]
    private static partial Regex LegendRegex();

    [GeneratedRegex(@"data-fd=""(?<name>\w+)""")]
    private static partial Regex DispositionRegex();

    [GeneratedRegex(@"ca7HeadPM ca7Head(?<name>\w+)""")]
    private static partial Regex DispositionClassRegex();

    [GeneratedRegex(@"class=""ca7Fixed tooltip""")]
    private static partial Regex FixedMarkerRegex();

    [GeneratedRegex(
        @"<div class=""caPmIntro"">(?<introHtml>.*?)</div>"
        + @"|<div class=""caPmHdr"">(?<hdrHtml>.*?)</div>"
        + @"|<div class=""caPmWhen""><b>WHEN:</b>(?<whenHtml>.*?)</div>"
        + @"|<div class=""caPmScore"">(?<scoreHtml>.*?)</div>"
        + @"|<div class=""caPmNote"">(?<noteHtml>.*?)</div>"
        + @"|<div class=""caAct""><div class=""caActHdr"">(?<actName>.*?)<span class=""caActIco""></span></div><div class=""caActType"">.*?</div><div class=""caActBody"">(?<actRows>(?:<div class=""caActRow"">.*?</div>)+)</div></div>",
        RegexOptions.Singleline)]
    private static partial Regex BlockRegex();

    [GeneratedRegex(@"^<span class=""caActLbl"">(?<label>.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"<div class=""caActRow"">(?<row>.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex ActionRowRegex();

    [GeneratedRegex(@"<span class=""caPm(?:Or|Plus)"">[^<]*</span>")]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"<span class=""caPmVP"">\+?(?<vp>\d+)VP</span>(?<labels>(?:<span class=""caPmCumul"">[^<]*</span>)*)")]
    private static partial Regex VictoryPointsRegex();

    [GeneratedRegex(@"<span class=""caPmCumul"">(?<label>[^<]*)</span>")]
    private static partial Regex LabelSpanRegex();

    [GeneratedRegex(@"^\(UP TO (?<cap>\d+)VP\)$")]
    private static partial Regex TurnCapRegex();
}
