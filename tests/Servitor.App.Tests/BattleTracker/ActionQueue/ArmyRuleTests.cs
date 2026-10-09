using Servitor.App.BattleTracker.ActionQueue;
using Servitor.App.BattleTracker.State;
using Servitor.App.Scoring.Tracking;
using Servitor.App.Tests.RulesData.Loading;

namespace Servitor.App.Tests.BattleTracker.ActionQueue;

public class AbilityTimingTests
{
    private static TurnContext Context(BattlePhase phase, Side active) => new(phase, active, Side.You);

    [Theory]
    [InlineData("In your Shooting phase, this unit can shoot twice.", BattlePhase.Shooting, Side.You, true)]
    [InlineData("In your Shooting phase, this unit can shoot twice.", BattlePhase.Shooting, Side.Enemy, false)]
    [InlineData("In your Shooting phase, this unit can shoot twice.", BattlePhase.Fight, Side.You, false)]
    [InlineData("In your opponent’s Charge phase, when a unit is chosen, do this.", BattlePhase.Charge, Side.Enemy, true)]
    [InlineData("In your opponent’s Charge phase, when a unit is chosen, do this.", BattlePhase.Charge, Side.You, false)]
    [InlineData("In the Fight phase, each time a unit fights, do this.", BattlePhase.Fight, Side.Enemy, true)]
    [InlineData("In your Shooting or Fight phase, do this.", BattlePhase.Fight, Side.You, true)]
    [InlineData("At the start of your Command phase, you can select one.", BattlePhase.Command, Side.You, true)]
    [InlineData("At the start of your Command phase, you can select one.", BattlePhase.Command, Side.Enemy, false)]
    public void A_phase_in_the_first_sentence_ties_an_ability_to_that_turn_and_phase(string text, BattlePhase phase, Side active, bool expected)
    {
        var timing = AbilityTiming.Of(text);

        Assert.NotNull(timing);
        Assert.Equal(expected, timing.IsActive(Context(phase, active)));
    }

    [Theory]
    [InlineData("At the start of the battle round, select one of the following.", TimingMoment.RoundStart)]
    [InlineData("At the end of the battle round, do this.", TimingMoment.RoundEnd)]
    [InlineData("At the start of your turn, do this.", TimingMoment.TurnStart)]
    [InlineData("At the end of your opponent’s turn, do this.", TimingMoment.TurnEnd)]
    public void A_moment_in_the_first_sentence_ties_an_ability_to_it(string text, TimingMoment expected)
    {
        Assert.Equal(expected, AbilityTiming.Of(text)!.Moment);
    }

    [Theory]
    [InlineData("This unit has +1 Toughness.")]
    [InlineData("While a friendly unit is within 6\" of this unit, it is brave.")]
    [InlineData("Each time this unit is selected to shoot, add 1 to hit rolls. This is useful in your Shooting phase.")]
    public void Text_without_a_phase_in_the_first_sentence_is_passive(string text)
    {
        Assert.Null(AbilityTiming.Of(text));
    }
}

public class RuleOptionParserTests
{
    [Fact]
    public void Block_alternatives_whose_name_runs_into_the_text_are_split_at_the_missing_space()
    {
        var options = RuleOptionParser.Parse(BattleTrackerFixtures.ChoiceHtml);

        Assert.Equal(["Fast Doctrine", "Hard Doctrine"], options.Select(o => o.Name));
        Assert.Equal("Go faster.", options[0].Text);
    }

    [Fact]
    public void Bullet_alternatives_with_a_bold_name_are_read()
    {
        const string html = "At the start of the battle round, select one of the following types.<br /><ul><li><b>Creeping Barrage:</b> Roll a D6.</li><li><b>Incendiary Bombardment:</b> Pick units.</li></ul>";

        var options = RuleOptionParser.Parse(html);

        Assert.Equal(["Creeping Barrage", "Incendiary Bombardment"], options.Select(o => o.Name));
        Assert.Equal("Roll a D6.", options[0].Text);
    }

    [Fact]
    public void A_rule_without_a_choice_prompt_or_with_one_alternative_is_not_a_choice()
    {
        Assert.Empty(RuleOptionParser.Parse("<ul><li><b>One:</b> a.</li><li><b>Two:</b> b.</li></ul>"));
        Assert.Empty(RuleOptionParser.Parse("Select one of the following.<div>Only OneJust this.</div>"));
    }
}

public class ArmyRuleCollectorTests
{
    private static IReadOnlyList<ArmyRuleEntry> Collect()
    {
        var army = BattleTrackerFixtures.OrkArmy();
        return ArmyRuleCollector.Collect(BattleTrackerFixtures.Data(), army.Setup, army.Units);
    }

    [Fact]
    public void Only_the_rules_of_the_armys_detachments_are_collected()
    {
        var detachmentRules = Collect().Where(e => e.Source == ArmyRuleSource.Detachment).ToList();

        var rule = Assert.Single(detachmentRules);
        Assert.Equal("Da Doctrines", rule.Title);
        Assert.True(rule.IsChoice);
        Assert.Equal(TimingMoment.None, rule.Timing!.Moment);
    }

    [Fact]
    public void Each_datasheet_is_listed_once_however_many_copies_are_fielded_and_attachment_abilities_are_skipped()
    {
        var unitRules = Collect().Where(e => e.Source == ArmyRuleSource.Unit).Select(e => e.Title).ToList();

        Assert.Equal(["Mob Rule", "Banner (Aura)", "Tough"], unitRules);
    }

    [Fact]
    public void Auras_are_recognised_by_their_name_tag_and_timed_abilities_get_their_timing()
    {
        var entries = Collect().ToDictionary(e => e.Title);

        Assert.True(entries["Banner (Aura)"].IsAura);
        Assert.False(entries["Tough"].IsAura);
        Assert.Null(entries["Tough"].Timing);
        Assert.NotNull(entries["Mob Rule"].Timing);
    }

    [Fact]
    public void Without_rules_data_nothing_is_collected()
    {
        var army = BattleTrackerFixtures.OrkArmy();

        Assert.Empty(ArmyRuleCollector.Collect(null, army.Setup, army.Units));
    }
}

public class ArmyRuleFilterTests
{
    private readonly ArmyRuleState _state = new(new InMemoryRulesDataStore());
    private readonly IReadOnlyList<ArmyRuleEntry> _entries;

    public ArmyRuleFilterTests()
    {
        var army = BattleTrackerFixtures.OrkArmy();
        _entries = ArmyRuleCollector.Collect(BattleTrackerFixtures.Data(), army.Setup, army.Units);
    }

    private ArmyRuleView View(BattlePhase phase, Side active) => ArmyRuleFilter.Filter(_entries, new TurnContext(phase, active, Side.You), _state);

    private static string[] Titles(IEnumerable<ArmyRuleEntry> entries) => entries.Select(e => e.Title).ToArray();

    [Fact]
    public void Timed_rules_are_active_only_at_their_moment_and_passive_ones_are_listed_apart()
    {
        var shooting = View(BattlePhase.Shooting, Side.You);
        Assert.Equal(["Mob Rule"], Titles(shooting.Active));
        Assert.Equal(["Banner (Aura)", "Tough"], Titles(shooting.Passive));

        Assert.Equal(["Da Doctrines"], Titles(View(BattlePhase.Command, Side.You).Active));
        Assert.Empty(View(BattlePhase.Movement, Side.You).Active);
        Assert.Empty(View(BattlePhase.Shooting, Side.Enemy).Active);
    }

    [Fact]
    public async Task Hiding_auras_removes_them_but_nothing_else()
    {
        await _state.SetShowAurasAsync(false);

        var view = View(BattlePhase.Shooting, Side.You);

        Assert.Equal(["Tough"], Titles(view.Passive));
        Assert.Equal(["Mob Rule"], Titles(view.Active));
    }

    [Fact]
    public async Task A_pinned_rule_stays_in_every_phase_and_survives_hidden_auras()
    {
        await _state.SetShowAurasAsync(false);
        await _state.PinAsync("unit|Boyz|Mob Rule");
        await _state.PinAsync("unit|Boyz|Banner (Aura)");

        var fight = View(BattlePhase.Fight, Side.Enemy);

        Assert.Equal(["Mob Rule", "Banner (Aura)"], Titles(fight.Active));
    }

    [Fact]
    public async Task A_rule_with_an_active_alternative_counts_as_pinned()
    {
        await _state.ActivateOptionAsync("detachment|Waaagh Tribe|Da Doctrines", "Fast Doctrine");

        Assert.Contains("Da Doctrines", Titles(View(BattlePhase.Fight, Side.Enemy).Active));
    }
}

public class ArmyRuleStateTests
{
    private readonly InMemoryRulesDataStore _store = new();

    [Fact]
    public async Task Pins_notes_auras_and_alternatives_survive_a_reload()
    {
        var before = new ArmyRuleState(_store);
        await before.SetShowAurasAsync(false);
        await before.PinAsync("r1");
        await before.SetPinNoteAsync("r1", "Unit X");
        await before.ActivateOptionAsync("r2", "Fast Doctrine");
        await before.ActivateOptionAsync("r2", "Hard Doctrine");

        var after = new ArmyRuleState(_store);
        await after.LoadAsync();

        Assert.False(after.ShowAuras);
        Assert.True(after.IsPinned("r1"));
        Assert.Equal("Unit X", after.PinNote("r1"));
        Assert.Equal("Hard Doctrine", after.ActiveOption("r2"));
        Assert.Equal(["Fast Doctrine"], after.UsedOptions("r2"));
    }

    [Fact]
    public async Task Ending_the_active_alternative_marks_it_used_and_unpinning_forgets_the_note()
    {
        var state = new ArmyRuleState(_store);
        await state.ActivateOptionAsync("r", "A");
        await state.EndOptionAsync("r");
        await state.PinAsync("p");
        await state.SetPinNoteAsync("p", "target");
        await state.UnpinAsync("p");

        Assert.Null(state.ActiveOption("r"));
        Assert.Equal(["A"], state.UsedOptions("r"));
        Assert.False(state.IsPinned("p"));
        Assert.Equal("", state.PinNote("p"));
    }
}
