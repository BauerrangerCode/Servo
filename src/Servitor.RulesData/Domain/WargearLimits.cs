namespace Servitor.RulesData.Domain;

/// <summary>Evaluates size-dependent wargear limits for a given model count.</summary>
public static class WargearLimits
{
    public static int At(WargearLimit limit, int models)
    {
        var value = limit.Base;
        foreach (var adjustment in limit.Adjustments.Where(a => a.Conditions.All(c => Holds(c, models))))
        {
            value = adjustment.Sets ? adjustment.Value : value + adjustment.Value;
        }

        return Math.Max(0, value);
    }

    /// <summary>How many models can take the pool's item in a unit of <paramref name="models"/> models: the smallest of its limits.</summary>
    public static int MaxFor(WargearPool pool, int models) =>
        pool.Limits.Count == 0 ? 0 : pool.Limits.Min(l => At(l, models));

    private static bool Holds(ModelCountCondition condition, int models) => condition.Operator switch
    {
        ModelCountOperator.AtLeast => models >= condition.Value,
        ModelCountOperator.GreaterThan => models > condition.Value,
        ModelCountOperator.AtMost => models <= condition.Value,
        ModelCountOperator.LessThan => models < condition.Value,
        _ => models == condition.Value,
    };
}
