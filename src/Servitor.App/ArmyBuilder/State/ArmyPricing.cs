using Servitor.App.ArmyBuilder.Catalog;
using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.State;

/// <summary>A unit of the army list with the price it costs there: <paramref name="Copy"/> is its number among the units of the same datasheet.</summary>
public sealed record PricedUnit(ArmyUnit Unit, int Copy, int Points, int WargearPoints);

/// <summary>
/// What units cost in an army. A datasheet's price can rise with the number of copies already taken ("1st to 2nd units
/// cost 90, 3rd and further cost 100"), so a unit's price depends on the units before it in the list. A unit the
/// datasheet has no price for keeps the points of the size it was added with. Chosen wargear adds its own points.
/// </summary>
public static class ArmyPricing
{
    /// <summary>The points the <paramref name="copy"/>-th unit of the datasheet costs at the given model count.</summary>
    public static int PointsFor(Datasheet datasheet, int models, int copy) =>
        FindPoints(datasheet, models, copy) ?? datasheet.Points;

    // Null when the datasheet has no price for that model count.
    private static int? FindPoints(Datasheet datasheet, int models, int copy)
    {
        var bracket = datasheet.CostBrackets.FirstOrDefault(b => b.Covers(copy)) ?? datasheet.CostBrackets.FirstOrDefault();
        var size = bracket?.Sizes.FirstOrDefault(s => s.Models == models)
            ?? UnitSizes.For(datasheet).FirstOrDefault(s => s.Models == models);
        return size?.Points;
    }

    /// <summary>Prices the list in order: each unit is the next copy of its datasheet.</summary>
    public static IReadOnlyList<PricedUnit> Price(IEnumerable<ArmyUnit> units)
    {
        var copies = new Dictionary<(string Name, string Faction), int>();
        var priced = new List<PricedUnit>();
        foreach (var unit in units)
        {
            var key = (unit.Datasheet.Name, unit.Datasheet.Faction);
            var copy = copies.GetValueOrDefault(key) + 1;
            copies[key] = copy;
            var wargearPoints = WargearConfig.Points(unit);
            var unitPoints = FindPoints(unit.Datasheet, unit.Size.Models, copy) ?? unit.Size.Points;
            priced.Add(new PricedUnit(unit, copy, unitPoints + wargearPoints, wargearPoints));
        }

        return priced;
    }
}
