using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.ArmyBuilder.Catalog;
using Servitor.App.ArmyBuilder.Components;
using Servitor.App.ArmyBuilder.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.App.Tests.RulesData.Loading;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.ArmyBuilder.Components;

public class AlliesPanelTests : BunitContext
{
    private readonly ArmyBuilderState _state = new();

    private static readonly AllyFaction[] Allies = [new("imperial-agents", "Imperial Agents"), new("imperial-knights", "Imperial Knights")];

    public AlliesPanelTests()
    {
        Services.AddSingleton<IArmyBuilderState>(_state);
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(
        [
            Units.Sheet("Captain", ["Character"], ["space-marines"], sizes: [new UnitSize(1, 80)]),
            Units.Sheet("Inquisitor", ["Character"], ["imperial-agents"], sizes: [new UnitSize(1, 60)]),
            Units.Sheet("Armiger Helverin", ["Vehicle"], ["imperial-knights"], sizes: [new UnitSize(1, 150)]),
        ]));
        _state.SelectBattleSize(BattleSizes.StrikeForce);
        _state.SelectFaction("space-marines", Allies.Select(a => a.Slug).ToList());
    }

    private IRenderedComponent<UnitCatalogPanel> RenderCatalog() => Render<UnitCatalogPanel>(p => p.Add(x => x.Allies, Allies));

    [Fact]
    public void The_allowed_ally_factions_are_named_and_offered_as_tabs()
    {
        var cut = RenderCatalog();

        Assert.Equal("Your army can also take: Imperial Agents · Imperial Knights", cut.Find(".allies-note").TextContent.Trim());
        Assert.Equal(["Own units", "Imperial Agents", "Imperial Knights"], cut.FindAll(".ally-tabs .chip").Select(c => c.TextContent.Trim()));
    }

    [Fact]
    public void An_army_without_allies_shows_no_ally_note_or_tabs()
    {
        _state.SelectFaction("space-marines", []);

        var cut = Render<UnitCatalogPanel>();

        Assert.Empty(cut.FindAll(".allies-note"));
        Assert.Empty(cut.FindAll(".ally-tabs"));
    }

    [Fact]
    public void An_ally_tab_lists_that_factions_units_and_adding_one_flags_it_as_allied()
    {
        var cut = RenderCatalog();
        Assert.Equal(["Captain"], cut.FindAll(".unit-name").Select(n => n.TextContent));

        cut.FindAll(".ally-tab")[0].Click();
        Assert.Equal(["Inquisitor"], cut.FindAll(".unit-name").Select(n => n.TextContent));

        cut.Find(".add-unit").Click();

        var unit = Assert.Single(_state.Units);
        Assert.Equal("imperial-agents", unit.AlliedFaction);
    }

    [Fact]
    public void Going_back_to_own_units_adds_units_without_the_flag()
    {
        var cut = RenderCatalog();
        cut.FindAll(".ally-tab")[0].Click();

        cut.Find(".ally-tabs .chip").Click(); // "Own units"
        cut.Find(".add-unit").Click();

        Assert.Null(_state.Units[0].AlliedFaction);
        Assert.Equal("Captain", _state.Units[0].Datasheet.Name);
    }

    [Fact]
    public void The_army_list_marks_allied_units_with_their_faction()
    {
        _state.AddUnit(Units.Sheet("Captain", factions: ["space-marines"]), new UnitSize(1, 80));
        _state.AddUnit(Units.Sheet("Inquisitor", factions: ["imperial-agents"]), new UnitSize(1, 60), "imperial-agents");

        var cut = Render<ArmyListPanel>(p => p.Add(x => x.Allies, Allies));

        var cards = cut.FindAll(".army-unit-card");
        Assert.Empty(cards[0].QuerySelectorAll(".army-unit-ally"));
        Assert.Equal("Ally: Imperial Agents", cards[1].QuerySelector(".army-unit-ally")!.TextContent.Trim());
    }
}
