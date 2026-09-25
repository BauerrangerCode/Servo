using Servitor.RulesData.Domain;

namespace Servitor.App.ArmyBuilder.Catalog;

/// <summary>How a catalog unit can be fielded: its sizes, and the model and points text the catalog shows for them.</summary>
public static class UnitSizes
{
    /// <summary>The unit's sizes, or one size at the datasheet's own points when the merge found no size list for it.</summary>
    public static IReadOnlyList<UnitSize> For(Datasheet datasheet) =>
        datasheet.Sizes.Count > 0 ? datasheet.Sizes : [new UnitSize(0, datasheet.Points)];

    /// <summary>"1", "10" or "5-10"; empty when the source has no model counts for the unit.</summary>
    public static string ModelsText(Datasheet datasheet) => Range(For(datasheet).Select(s => s.Models).Where(m => m > 0));

    public static string PointsText(Datasheet datasheet) => Range(For(datasheet).Select(s => s.Points));

    private static string Range(IEnumerable<int> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return "";
        }

        var (min, max) = (list.Min(), list.Max());
        return min == max ? $"{min}" : $"{min}-{max}";
    }
}
