using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.Rules;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.App.Tests.ArmyBuilder.Validation;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.ArmyBuilder.Rules;

internal sealed class FakeRuleProvider(DetachmentRuleSet? rules = null) : IDetachmentRuleProvider
{
    public DetachmentRuleSet Current { get; set; } = rules ?? DetachmentRuleSet.Empty;

    public int Loads { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Loads++;
        return Task.CompletedTask;
    }
}

public class DetachmentRuleProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(respond(request));
        }
    }

    private const string OneRule = "{\"rules\":[{\"faction\":\"a\",\"detachment\":\"b\",\"type\":\"cannotInclude\",\"label\":\"c\"}]}";

    [Fact]
    public async Task The_rules_file_is_fetched_and_parsed()
    {
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OneRule, Encoding.UTF8) });
        var provider = new HttpDetachmentRuleProvider(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

        Assert.Empty(provider.Current.Rules);
        await provider.LoadAsync();

        Assert.Single(provider.Current.Rules);
        Assert.Equal("/rules/detachment-rules.json", handler.Urls.Single());
    }

    [Fact]
    public async Task A_file_that_cannot_be_fetched_leaves_no_rules_and_says_why()
    {
        var missing = new HttpDetachmentRuleProvider(new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))) { BaseAddress = new Uri("http://localhost/") });
        await missing.LoadAsync();
        Assert.Empty(missing.Current.Rules);
        Assert.Contains("404", missing.Current.Warnings.Single(), StringComparison.Ordinal);

        var offline = new HttpDetachmentRuleProvider(new HttpClient(new Handler(_ => throw new HttpRequestException("offline"))) { BaseAddress = new Uri("http://localhost/") });
        await offline.LoadAsync();
        Assert.Contains("offline", offline.Current.Warnings.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loading_again_picks_up_a_changed_file()
    {
        var body = OneRule;
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        var provider = new HttpDetachmentRuleProvider(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
        await provider.LoadAsync();

        body = "{\"rules\":[]}";
        await provider.LoadAsync();

        Assert.Empty(provider.Current.Rules);
    }

    // ---- the shipped rules file --------------------------------------------------------------------------------

    private static string RulesFile => Path.Combine(TestPaths.RepoRoot, "src", "Servitor.App", "wwwroot", "rules", "detachment-rules.json");

    [Fact]
    public void The_shipped_rules_file_is_read_without_a_warning_and_covers_all_rule_types()
    {
        var set = DetachmentRuleParser.Parse(File.ReadAllText(RulesFile));

        Assert.Empty(set.Warnings);
        Assert.NotEmpty(set.Rules.OfType<MustIncludeRule>());
        Assert.NotEmpty(set.Rules.OfType<CannotIncludeRule>());
        Assert.NotEmpty(set.Rules.OfType<PointsCapRule>());
        Assert.NotEmpty(set.Rules.OfType<UnitLimitOverrideRule>());
    }

    [Fact]
    public void Every_rule_of_the_shipped_file_names_a_detachment_that_exists_in_the_rules_data()
    {
        var set = DetachmentRuleParser.Parse(File.ReadAllText(RulesFile));
        using var data = JsonDocument.Parse(File.ReadAllText(TestPaths.WwwrootDataFile("merged", "rules-data.json")));
        var detachments = data.RootElement.GetProperty("Factions").EnumerateArray()
            .SelectMany(f => f.GetProperty("Detachments").EnumerateArray()
                .Select(d => (Faction: f.GetProperty("FactionId").GetString()!, Name: NameNormalizer.Normalize(d.GetProperty("Name").GetString()!))))
            .ToHashSet();

        Assert.All(set.Rules, rule => Assert.True(
            detachments.Contains((rule.Faction, NameNormalizer.Normalize(rule.Detachment))),
            $"{rule.Faction}/{rule.Detachment} is not a detachment of the rules data."));
    }

    [Fact]
    public void The_named_units_of_the_shipped_rules_exist_in_the_rules_data()
    {
        var set = DetachmentRuleParser.Parse(File.ReadAllText(RulesFile));
        using var data = JsonDocument.Parse(File.ReadAllText(TestPaths.WwwrootDataFile("merged", "rules-data.json")));
        var names = data.RootElement.GetProperty("Datasheets").EnumerateArray()
            .Select(d => NameNormalizer.Normalize(d.GetProperty("Name").GetString()!))
            .ToHashSet();

        var required = set.Rules.SelectMany(r => r.Units.Names).Distinct().Where(n => !names.Contains(NameNormalizer.Normalize(n))).ToList();

        Assert.Empty(required);
    }
}

public class DetachmentRuleIntegrationTests : BunitContext
{
    private static readonly MustIncludeRule Houndpack = new(
        "chaos-knights", "Houndpack Lance", "WAR DOG units", RuleData.Selector(keywords: ["WAR DOG"]), 3);

    private ArmyBuilderState PrepareState(DetachmentRuleSet rules)
    {
        var state = new ArmyBuilderState(new FakeRuleProvider(rules));
        state.SelectBattleSize(BattleSizes.StrikeForce);
        state.SelectFaction("chaos-knights");
        state.AddDetachment(Army.Detachment("Houndpack Lance"));
        Services.AddSingleton<IArmyBuilderState>(state);
        return state;
    }

    [Fact]
    public void A_missing_required_unit_shows_up_in_the_summary_as_its_own_issue()
    {
        var state = PrepareState(RuleData.Set(Houndpack));
        state.AddUnit(RuleData.Sheet("War Dog Stalker", ["WAR DOG"]), new UnitSize(1, 100));

        var cut = Render<ArmySummary>();

        Assert.Contains("INVALID", cut.Find(".army-validity").TextContent);
        Assert.Contains("needs at least 3 WAR DOG units in the army, but it has 1", cut.Find(".army-issues").TextContent);
    }

    [Fact]
    public void Adding_the_required_units_makes_the_army_valid()
    {
        var state = PrepareState(RuleData.Set(Houndpack));
        var cut = Render<ArmySummary>();
        Assert.Contains("INVALID", cut.Find(".army-validity").TextContent);

        for (var i = 0; i < 3; i++)
        {
            state.AddUnit(RuleData.Sheet("War Dog Stalker", ["WAR DOG"]), new UnitSize(1, 100));
        }

        cut.WaitForAssertion(() => Assert.Equal("VALID", cut.Find(".army-validity").TextContent.Trim()));
    }

    [Fact]
    public void Problems_with_the_rules_file_are_shown_so_a_broken_rule_does_not_go_unnoticed()
    {
        PrepareState(new DetachmentRuleSet([], ["Rule 2 (Houndpack Lance): has the unknown type \"mustInclud\"."]));

        var cut = Render<ArmySummary>();

        Assert.Contains("unknown type", cut.Find(".army-rule-warnings").TextContent);
    }

    [Fact]
    public void A_state_without_a_rule_provider_has_no_rules()
    {
        Assert.Same(DetachmentRuleSet.Empty, new ArmyBuilderState().Rules);
    }
}
