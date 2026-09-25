using System.Globalization;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>
/// The running totals of an army list. A unit whose model count the source does not give counts as one model;
/// wounds are models times the Wounds characteristic of the datasheet's stat line (a non-numeric value counts as 0).
/// </summary>
public sealed record ArmyTotals(int Points, int Units, int Models, int Wounds)
{
    public static ArmyTotals Empty { get; } = new(0, 0, 0, 0);

    public static ArmyTotals Of(IReadOnlyList<PricedUnit> units)
    {
        var models = 0;
        var wounds = 0;
        foreach (var priced in units)
        {
            var unitModels = Math.Max(1, priced.Unit.Size.Models);
            models += unitModels;
            wounds += unitModels * WoundsPerModel(priced.Unit.Datasheet.StatLine.Wounds);
        }

        return new ArmyTotals(units.Sum(u => u.Points), units.Count, models, wounds);
    }

    private static int WoundsPerModel(string wounds) =>
        int.TryParse(wounds, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
