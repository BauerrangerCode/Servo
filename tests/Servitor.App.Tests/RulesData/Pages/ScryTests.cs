using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.RulesData.Loading;
using Servitor.App.RulesData.Pages;
using Servitor.App.Shared.Backdrop;
using Servitor.App.Shared.Pager;
using Servitor.App.Tests.Shared.Pager;
using Servitor.RulesData.Domain;

namespace Servitor.App.Tests.RulesData.Pages;

public class ScryTests : BunitContext
{
    private readonly FactionBackdrop _backdrop = new();

    private readonly FakePagerInterop _pager = new();

    public ScryTests()
    {
        Services.AddSingleton<IFactionBackdrop>(_backdrop);
        Services.AddSingleton<IPagerInterop>(_pager);
    }

    private sealed class FakeDatasheetCatalog(IReadOnlyList<Datasheet> datasheets) : IDatasheetCatalog
    {
        private IReadOnlyList<Datasheet> _datasheets = datasheets;

        public event Action? Changed;

        public Task<IReadOnlyList<Datasheet>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_datasheets);

        public void Replace(IReadOnlyList<Datasheet> datasheets)
        {
            _datasheets = datasheets;
            Changed?.Invoke();
        }
    }

    private static Datasheet Sheet(string name, string faction) => new(
        name,
        faction,
        70,
        new StatLine("6\"", "3", "5+", null, "1", "7+", "2"),
        [],
        [],
        [],
        [],
        [],
        ["Infantry"],
        null,
        []);

    [Fact]
    public void Renders_the_result_count_and_every_loaded_datasheet()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(
        [
            Sheet("Cadian Shock Troops", "Astra Militarum"),
            Sheet("Boyz", "Orks"),
        ]));

        var cut = Render<Scry>();

        Assert.Contains("2 results", cut.Markup);
        Assert.Contains("Cadian Shock Troops", cut.Markup);
        Assert.Contains("Boyz", cut.Markup);
    }

    [Fact]
    public void Clicking_a_faction_chip_makes_that_faction_the_backdrop()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([Sheet("Boyz", "Xenos - Orks")]));
        var cut = Render<Scry>();

        cut.FindAll(".facet-group")
            .First(g => g.QuerySelector("h4")!.TextContent == "Faction")
            .QuerySelector(".chip")!.Click();

        Assert.Equal("orks", _backdrop.FactionSlug);
    }

    [Fact]
    public void Typing_a_query_filters_the_result_list_and_count()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(
        [
            Sheet("Cadian Shock Troops", "Astra Militarum"),
            Sheet("Boyz", "Orks"),
        ]));

        var cut = Render<Scry>();

        cut.Find("input[type=text]").Input("Boyz");

        Assert.Contains("1 result", cut.Markup);
        Assert.DoesNotContain("Cadian Shock Troops", cut.Markup);
    }

    [Fact]
    public void Clicking_a_role_chip_cycles_through_include_and_exclude()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(
        [
            Sheet("Cadian Shock Troops", "Astra Militarum"),
        ]));

        var cut = Render<Scry>();

        AngleSharp.Dom.IElement FindCharacterChip() =>
            cut.FindAll("button.chip").First(b => b.TextContent.Trim() == "Character");

        FindCharacterChip().Click();
        Assert.Contains("chip-include", FindCharacterChip().ClassList);

        FindCharacterChip().Click();
        Assert.Contains("chip-exclude", FindCharacterChip().ClassList);

        FindCharacterChip().Click();
        Assert.Contains("chip-neutral", FindCharacterChip().ClassList);
    }

    [Fact]
    public void Splits_the_page_into_filters_units_and_datasheet_screens()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([Sheet("Boyz", "Orks")]));

        var cut = Render<Scry>();

        Assert.Equal(["Filters", "Units", "Datasheet"], cut.FindAll(".pager-screen").Select(s => s.GetAttribute("aria-label")));
        Assert.Single(cut.Find(".pager-screen[aria-label=Units]").QuerySelectorAll("input[type=text]"));
        var filters = cut.Find(".pager-screen[aria-label=Filters]");
        Assert.NotEmpty(filters.QuerySelectorAll(".chip"));
        Assert.Empty(filters.QuerySelectorAll("input[type=text]"));
    }

    [Fact]
    public void Tapping_a_unit_selects_it_and_scrolls_to_the_datasheet_screen()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([Sheet("Boyz", "Orks")]));
        var cut = Render<Scry>();

        cut.Find("td.cell-name").Click();

        Assert.Equal([2], _pager.ScrolledTo);
        Assert.Contains("Boyz", cut.Find(".pager-screen[aria-label=Datasheet]").TextContent);
        Assert.Single(cut.FindAll(".result-list tr.selected"));
    }

    [Fact]
    public void The_faction_filter_comes_before_the_role_filter()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([Sheet("Boyz", "Orks")]));

        var cut = Render<Scry>();

        var headings = cut.FindAll(".facet-group h4").Select(h => h.TextContent).ToList();
        Assert.True(headings.IndexOf("Faction") < headings.IndexOf("Role"));
    }

    [Fact]
    public void The_datasheet_screen_is_not_reachable_until_a_unit_is_selected()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([Sheet("Boyz", "Orks")]));

        var cut = Render<Scry>();

        Assert.Contains("pager-screen-disabled", cut.Find(".pager-screen[aria-label=Datasheet]").ClassList);
        Assert.Equal(2, cut.FindAll(".pager-dot").Count);

        cut.Find("td.cell-name").Click();

        Assert.DoesNotContain("pager-screen-disabled", cut.Find(".pager-screen[aria-label=Datasheet]").ClassList);
        Assert.Equal(3, cut.FindAll(".pager-dot").Count);
    }

    [Fact]
    public void Changing_a_filter_forgets_the_selected_unit_and_locks_the_datasheet_screen_again()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog(
        [
            Sheet("Cadian Shock Troops", "Astra Militarum"),
            Sheet("Boyz", "Orks"),
        ]));
        var cut = Render<Scry>();
        cut.FindAll("td.cell-name").First().Click();

        cut.Find("input[type=text]").Input("Boyz");

        Assert.Empty(cut.FindAll(".result-list tr.selected"));
        Assert.Contains("pager-screen-disabled", cut.Find(".pager-screen[aria-label=Datasheet]").ClassList);
    }

    [Fact]
    public void Shows_a_merge_hint_instead_of_an_empty_result_table_when_no_data_exists()
    {
        Services.AddSingleton<IDatasheetCatalog>(new FakeDatasheetCatalog([]));

        var cut = Render<Scry>();

        Assert.Contains("Update data", cut.Find(".scry-empty").TextContent);
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void Reloads_the_result_list_when_the_catalog_reports_new_data()
    {
        var catalog = new FakeDatasheetCatalog([]);
        Services.AddSingleton<IDatasheetCatalog>(catalog);
        var cut = Render<Scry>();

        cut.InvokeAsync(() => catalog.Replace([Sheet("Boyz", "Orks")]));

        cut.WaitForAssertion(() => Assert.Contains("1 result", cut.Markup));
        Assert.Contains("Boyz", cut.Markup);
    }
}
