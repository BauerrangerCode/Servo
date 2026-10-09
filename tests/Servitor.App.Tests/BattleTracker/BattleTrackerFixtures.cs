using Servitor.App.ArmyBuilder.State;
using Servitor.App.RulesData.Loading;
using Servitor.App.Tests.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;
using Servitor.RulesData.Merging;

namespace Servitor.App.Tests.BattleTracker;

internal static class BattleTrackerFixtures
{
    public const string DetachmentName = "Waaagh Tribe";

    public static Stratagem Stratagem(string id, string name, string phase, string turn = "Your turn", int cp = 1, string detachment = "", GameMode mode = GameMode.Standard, string when = "When text.") =>
        new(id, name, StratagemCategory.BattleTactic, cp, "Flavor.", turn, phase, when, "Target text.", "Effect text.", "", "", detachment, mode);

    public static MergedRulesData Data() => new(
        "v1",
        DateTimeOffset.UnixEpoch,
        [],
        [
            Stratagem("c1", "COMMAND RE-ROLL", "Any phase", "Either player’s turn"),
            Stratagem("c2", "FIRE OVERWATCH", "Movement phase", "Opponent’s turn"),
            Stratagem("c3", "GRENADE", "Shooting phase"),
            Stratagem("c4", "BOARDING ONLY", "Shooting phase", mode: GameMode.BoardingActions),
            Stratagem("c5", "SMOKESCREEN", "Shooting phase", "Opponent’s turn"),
            Stratagem("c6", "ANY FIGHT", "Fight phase", "Either player’s turn"),
            Stratagem("c7", "MOVE OUT", "Any phase", "Opponent’s turn", when: "End of your opponent’s turn."),
            Stratagem("c8", "ROUND OPENER", "Any phase", "Either player’s turn", when: "Start of the battle round."),
            Stratagem("c9", "ROUND CLOSER", "Any phase", "Either player’s turn", when: "End of the battle round."),
            Stratagem("c10", "MY TURN OPENER", "Any phase", when: "Start of your turn."),
        ],
        [
            new MergedFactionRules("orks", "Orks",
            [
                Stratagem("d1", "BIG KRUNCH", "Shooting or Charge phase", detachment: DetachmentName),
                Stratagem("d2", "OTHER DETACHMENT", "Shooting phase", detachment: "Blitz Brigade"),
            ], [], [], []),
        ],
        []);

    public static ArmyBuilderState OrkArmy()
    {
        var state = new ArmyBuilderState();
        state.SelectBattleSize(BattleSizes.StrikeForce);
        state.SelectFaction("orks");
        state.AddDetachment(new Detachment("w", DetachmentName, "", 1, MissionArchetype.TakeAndHold, 4, GameMode.Standard));
        state.AddUnit(Units.Sheet("Boyz", factions: ["orks"]), new UnitSize(10, 90));
        return state;
    }

    public sealed class FakeRulesDataProvider(MergedRulesData? data) : IMergedRulesDataProvider
    {
        public event Action? DataChanged
        {
            add { }
            remove { }
        }

        public Task<MergedRulesData?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(data);

        public Task StoreAsync(MergedRulesData value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
