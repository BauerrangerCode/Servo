using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Servitor.App.Shared.Pager;

namespace Servitor.App.Tests.Shared.Pager;

public class ScreenPagerTests : BunitContext
{
    private readonly FakePagerInterop _interop = new();

    public ScreenPagerTests() => Services.AddSingleton<IPagerInterop>(_interop);

    private IRenderedComponent<ScreenPager> RenderPager() => Render<ScreenPager>(parameters => parameters
        .AddChildContent<PagerScreen>(screen => screen.Add(p => p.Title, "Filters").AddChildContent("<p>one</p>"))
        .AddChildContent<PagerScreen>(screen => screen.Add(p => p.Title, "Units").AddChildContent("<p>two</p>"))
        .AddChildContent<PagerScreen>(screen => screen.Add(p => p.Title, "Datasheet").AddChildContent("<p>three</p>")));

    [Fact]
    public void Renders_every_screen_with_a_dot_each_and_starts_on_the_first()
    {
        var cut = RenderPager();

        Assert.Equal(3, cut.FindAll(".pager-screen").Count);
        Assert.Equal(3, cut.FindAll(".pager-dot").Count);
        Assert.Equal("Filters", cut.Find(".pager-title").TextContent);
        Assert.Contains("active", cut.FindAll(".pager-dot")[0].ClassList);
    }

    [Fact]
    public void The_previous_arrow_is_disabled_on_the_first_screen_and_the_next_arrow_on_the_last()
    {
        var cut = RenderPager();
        Assert.True(cut.Find(".pager-prev").HasAttribute("disabled"));
        Assert.False(cut.Find(".pager-next").HasAttribute("disabled"));

        cut.Find(".pager-next").Click();
        cut.Find(".pager-next").Click();

        Assert.False(cut.Find(".pager-prev").HasAttribute("disabled"));
        Assert.True(cut.Find(".pager-next").HasAttribute("disabled"));
    }

    [Fact]
    public void The_next_arrow_scrolls_to_the_following_screen_and_updates_the_title()
    {
        var cut = RenderPager();

        cut.Find(".pager-next").Click();

        Assert.Equal([1], _interop.ScrolledTo);
        Assert.Equal("Units", cut.Find(".pager-title").TextContent);
    }

    [Fact]
    public void A_dot_jumps_straight_to_its_screen()
    {
        var cut = RenderPager();

        cut.FindAll(".pager-dot")[2].Click();

        Assert.Equal([2], _interop.ScrolledTo);
        Assert.Equal("Datasheet", cut.Find(".pager-title").TextContent);
    }

    [Fact]
    public async Task A_swipe_that_settles_on_another_screen_updates_the_controls_without_scrolling_again()
    {
        var cut = RenderPager();

        await cut.InvokeAsync(() => _interop.SwipeToAsync(1));

        Assert.Equal("Units", cut.Find(".pager-title").TextContent);
        Assert.Contains("active", cut.FindAll(".pager-dot")[1].ClassList);
        Assert.Empty(_interop.ScrolledTo);
    }

    [Fact]
    public async Task GoToAsync_ignores_an_index_outside_the_screens()
    {
        var cut = RenderPager();

        await cut.InvokeAsync(() => cut.Instance.GoToAsync(3));
        await cut.InvokeAsync(() => cut.Instance.GoToAsync(-1));

        Assert.Empty(_interop.ScrolledTo);
        Assert.Equal("Filters", cut.Find(".pager-title").TextContent);
    }

    [Fact]
    public async Task Disposing_the_pager_detaches_the_scroll_listener()
    {
        var cut = RenderPager();

        await DisposeComponentsAsync();

        Assert.Equal(1, _interop.Detachments);
        Assert.NotNull(cut);
    }

    private IRenderedComponent<ScreenPager> RenderPagerWithDisabledLastScreen(bool lastEnabled) => Render<ScreenPager>(parameters => parameters
        .AddChildContent<PagerScreen>(screen => screen.Add(p => p.Title, "Units").AddChildContent("<p>one</p>"))
        .AddChildContent<PagerScreen>(screen => screen.Add(p => p.Title, "Datasheet").Add(p => p.Enabled, lastEnabled).AddChildContent("<p>two</p>")));

    [Fact]
    public async Task A_disabled_screen_has_no_dot_and_cannot_be_reached()
    {
        var cut = RenderPagerWithDisabledLastScreen(lastEnabled: false);

        await cut.InvokeAsync(() => cut.Instance.GoToAsync(1));

        Assert.Single(cut.FindAll(".pager-dot"));
        Assert.True(cut.Find(".pager-next").HasAttribute("disabled"));
        Assert.Empty(_interop.ScrolledTo);
    }

    [Fact]
    public async Task A_swipe_cannot_settle_on_a_disabled_screen()
    {
        var cut = RenderPagerWithDisabledLastScreen(lastEnabled: false);

        await cut.InvokeAsync(() => _interop.SwipeToAsync(1));

        Assert.Equal("Units", cut.Find(".pager-title").TextContent);
    }

    [Fact]
    public void Disabling_the_current_screen_falls_back_to_the_last_reachable_one()
    {
        var enabled = true;
        var cut = Render<ScreenPager>(parameters => parameters
            .Add(p => p.ChildContent, builder =>
            {
                builder.OpenComponent<PagerScreen>(0);
                builder.AddComponentParameter(1, nameof(PagerScreen.Title), "Units");
                builder.CloseComponent();
                builder.OpenComponent<PagerScreen>(2);
                builder.AddComponentParameter(3, nameof(PagerScreen.Title), "Datasheet");
                builder.AddComponentParameter(4, nameof(PagerScreen.Enabled), enabled);
                builder.CloseComponent();
            }));
        cut.Find(".pager-next").Click();
        Assert.Equal("Datasheet", cut.Find(".pager-title").TextContent);
        _interop.ScrolledTo.Clear();

        enabled = false;
        cut.Render();

        Assert.Equal("Units", cut.Find(".pager-title").TextContent);
        Assert.Equal([0], _interop.ScrolledTo);
    }
}
