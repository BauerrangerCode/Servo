using Servitor.RulesData.Domain;
using Servitor.RulesData.Mapping.BsData;

namespace Servitor.RulesData.Mapping;

/// <summary>
/// Reads the wargear a unit can configure out of its BSData selection tree into two shapes: weapon slots of unique
/// models (a group of which exactly one choice is taken, e.g. a sergeant's "Weapon 1") and pools (a number of
/// identical models take a weapon, e.g. "Boy w/ Big shoota", at most 2). Limits that grow with the unit's size are kept
/// as adjustments. Everything else in the tree — nested loadout packages, optional single upgrades, choices per
/// non-unique model, hidden options — is left out.
/// </summary>
public static class BsDataWargearMapper
{
    private const string ModelType = "model";
    private const string SelectionsField = "selections";
    private const string ModelChildId = "model";

    // Nodes of the selection tree that hold campaign or army-list data, not wargear.
    private static readonly string[] IgnoredNamePrefixes =
        ["Crusade", "Enhancement", "Warlord", "Battle", "Legendary", "Experience", "Damned", "Lone Wolf", "Weapon Modifications", "Weapon Upgrades"];

    private static readonly Dictionary<string, ModelCountOperator> Operators = new()
    {
        ["atLeast"] = ModelCountOperator.AtLeast,
        ["greaterThan"] = ModelCountOperator.GreaterThan,
        ["atMost"] = ModelCountOperator.AtMost,
        ["lessThan"] = ModelCountOperator.LessThan,
        ["equalTo"] = ModelCountOperator.EqualTo,
    };

    /// <returns>The unit's wargear options, or null when it has none in the two supported shapes.</returns>
    public static WargearOptions? Map(BsEntry unit, IReadOnlyDictionary<string, BsEntry> entriesById)
    {
        var root = new Node(unit, unit.Name, unit.Constraints, unit.Modifiers, unit.Id, null);
        var models = new List<(Node Model, Node? Group)>();
        FindModels(root, null, entriesById, [], models);

        var uniqueModels = new List<WargearModel>();
        var pools = new List<WargearPool>();

        if (unit.Type == ModelType)
        {
            AddUniqueModel(root, entriesById, uniqueModels);
        }

        var bases = models.Where(m => Value(m.Model, "min", "parent") is > 0 && !IsUnique(m.Model)).Select(m => m.Model).ToList();
        foreach (var (model, group) in models)
        {
            if (IsUnique(model))
            {
                AddUniqueModel(model, entriesById, uniqueModels);
            }
            else if (Value(model, "min", "parent") is null or 0 && Value(model, "max", "parent") is > 0)
            {
                AddPool(model, group, bases, entriesById, pools);
            }
        }

        var options = new WargearOptions(uniqueModels, pools);
        return options.IsEmpty ? null : options;
    }

    // ---- tree --------------------------------------------------------------------------------------------------

    /// <summary>
    /// A selection-tree node with the constraints and modifiers of its entry link merged in: a link narrows what its
    /// target says (a shared "Bolt pistol" entry gets the link's own max).
    /// </summary>
    private sealed record Node(
        BsEntry Entry,
        string Name,
        IReadOnlyList<BsConstraint> Constraints,
        IReadOnlyList<BsModifier> Modifiers,
        string Id,
        string? TargetId);

    private static IEnumerable<Node> Children(Node node, IReadOnlyDictionary<string, BsEntry> entriesById)
    {
        foreach (var link in node.Entry.EntryLinks)
        {
            if (link.TargetId is { Length: > 0 } targetId && entriesById.TryGetValue(targetId, out var target))
            {
                yield return new Node(
                    target,
                    string.IsNullOrEmpty(link.Name) ? target.Name : link.Name,
                    [.. link.Constraints, .. target.Constraints],
                    [.. link.Modifiers, .. target.Modifiers],
                    link.Id,
                    targetId);
            }
        }

        foreach (var child in node.Entry.SelectionEntries.Concat(node.Entry.SelectionEntryGroups))
        {
            yield return new Node(child, child.Name, child.Constraints, child.Modifiers, child.Id, null);
        }
    }

    private static bool IsIgnored(Node node) => IgnoredNamePrefixes.Any(p => node.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static bool IsGroup(Node node) => node.Entry.Type is null;

    private static void FindModels(
        Node node,
        Node? enclosingGroup,
        IReadOnlyDictionary<string, BsEntry> entriesById,
        HashSet<string> path,
        List<(Node Model, Node? Group)> models)
    {
        if (!path.Add(node.Entry.Id))
        {
            return;
        }

        foreach (var child in Children(node, entriesById).Where(c => !IsIgnored(c)))
        {
            if (child.Entry.Type == ModelType)
            {
                models.Add((child, enclosingGroup));
            }
            else if (IsGroup(child))
            {
                FindModels(child, child, entriesById, path, models);
            }
        }

        path.Remove(node.Entry.Id);
    }

    // ---- constraints and limits --------------------------------------------------------------------------------

    private static BsConstraint? Constraint(Node node, string type, string scope) =>
        node.Constraints.FirstOrDefault(c =>
            c.Field == SelectionsField && c.Type == type && c.Scope == scope && c.Value.AsInt() is not null and not -1);

    private static int? Value(Node node, string type, string scope) => Constraint(node, type, scope)?.Value.AsInt();

    private static WargearLimit? LimitOf(Node node, string type, string scope)
    {
        if (Constraint(node, type, scope) is not { } constraint || constraint.Value.AsInt() is not { } baseValue)
        {
            return null;
        }

        var adjustments = new List<LimitAdjustment>();
        foreach (var modifier in node.Modifiers.Where(m => m.Field == constraint.Id))
        {
            if (Adjustment(modifier) is { } adjustment)
            {
                adjustments.Add(adjustment);
            }
        }

        return new WargearLimit(baseValue, adjustments);
    }

    // Only modifiers whose conditions are all about the unit's model count can be evaluated for a chosen size.
    private static LimitAdjustment? Adjustment(BsModifier modifier)
    {
        if (modifier.ConditionGroups.Count > 0 || modifier.Conditions.Count == 0 || modifier.Value.AsInt() is not { } value)
        {
            return null;
        }

        var sets = modifier.Type == "set";
        if (!sets && modifier.Type is not ("increment" or "decrement"))
        {
            return null;
        }

        var conditions = new List<ModelCountCondition>();
        foreach (var condition in modifier.Conditions)
        {
            if (condition.ChildId != ModelChildId
                || condition.Field != SelectionsField
                || condition.Type is null
                || !Operators.TryGetValue(condition.Type, out var op)
                || condition.Value.AsInt() is not { } threshold)
            {
                return null;
            }

            conditions.Add(new ModelCountCondition(op, threshold));
        }

        return new LimitAdjustment(sets, modifier.Type == "decrement" ? -value : value, conditions);
    }

    // ---- unique models: weapon slots ---------------------------------------------------------------------------

    private static bool IsUnique(Node model) => Value(model, "min", "parent") == 1 && Value(model, "max", "parent") == 1;

    private static void AddUniqueModel(Node model, IReadOnlyDictionary<string, BsEntry> entriesById, List<WargearModel> uniqueModels)
    {
        var slots = SlotsIn(model, entriesById, []);

        if (slots.Count > 0)
        {
            uniqueModels.Add(new WargearModel(model.Name, slots));
        }
    }

    // The slots directly under the node, or under container groups (a "Wargear" group without limits of its own).
    private static List<WargearSlot> SlotsIn(Node node, IReadOnlyDictionary<string, BsEntry> entriesById, HashSet<string> path)
    {
        var slots = new List<WargearSlot>();
        if (!path.Add(node.Entry.Id))
        {
            return slots;
        }

        foreach (var group in Children(node, entriesById).Where(c => IsGroup(c) && !IsIgnored(c)))
        {
            if (SlotOf(group, entriesById) is { } slot)
            {
                slots.Add(slot);
            }
            else if (Value(group, "min", "parent") is null && Value(group, "max", "parent") is null)
            {
                slots.AddRange(SlotsIn(group, entriesById, path));
            }
        }

        path.Remove(node.Entry.Id);
        return slots;
    }

    // A group of which exactly one choice is taken. A group whose choices hold options of their own (a loadout package
    // that has its own weapon groups) is not offered, since choosing it would leave those options unreachable.
    private static WargearSlot? SlotOf(Node group, IReadOnlyDictionary<string, BsEntry> entriesById)
    {
        if (Value(group, "min", "parent") != 1 || Value(group, "max", "parent") != 1)
        {
            return null;
        }

        var choices = Children(group, entriesById).Where(c => !IsIgnored(c)).ToList();
        if (choices.Count < 2 || choices.Any(c => IsGroup(c) || Children(c, entriesById).Any(child => IsGroup(child) && !IsIgnored(child))))
        {
            return null;
        }

        var defaultId = group.Entry.DefaultSelectionEntryId;
        var defaultIndex = defaultId is null ? -1 : choices.FindIndex(c => c.Id == defaultId || c.TargetId == defaultId || c.Entry.Id == defaultId);
        return new WargearSlot(group.Name, choices.Select(c => c.Name).ToList(), Math.Max(0, defaultIndex));
    }

    // ---- pools: models that take a weapon --------------------------------------------------------------------

    private static void AddPool(
        Node variant,
        Node? group,
        IReadOnlyList<Node> bases,
        IReadOnlyDictionary<string, BsEntry> entriesById,
        List<WargearPool> pools)
    {
        var upgrades = Children(variant, entriesById).Where(c => c.Entry.Type == "upgrade").ToList();
        var baseNames = bases
            .SelectMany(b => Children(b, entriesById).Where(c => c.Entry.Type == "upgrade").Select(c => c.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var variantNames = upgrades.Select(u => u.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var distinguishing = upgrades.Where(u => !baseNames.Contains(u.Name)).ToList();
        var item = distinguishing.Count > 0 ? string.Join(" + ", distinguishing.Select(u => u.Name)) : ItemFromModelName(variant.Name);
        var replaced = baseNames.Except(variantNames, StringComparer.OrdinalIgnoreCase).ToList();

        var limits = new List<WargearLimit>();
        if (LimitOf(variant, "max", "parent") is { } perModel)
        {
            limits.Add(perModel);
        }

        if (distinguishing.Count > 0 && LimitOf(distinguishing[0], "max", "unit") is { } perUnit)
        {
            limits.Add(perUnit);
        }

        // A group of optional models ("Special Weapons": at most 3 of them) caps the pool too; the group that holds the
        // unit's regular models (it has a minimum) is the unit size, not a wargear limit.
        if (group is not null && Value(group, "min", "parent") is null or 0 && LimitOf(group, "max", "parent") is { } perGroup)
        {
            limits.Add(perGroup);
        }

        if (limits.Count > 0)
        {
            pools.Add(new WargearPool(item, replaced.Count > 0 ? string.Join(", ", replaced) : null, limits));
        }
    }

    // "Boy w/ Big shoota" -> "Big shoota".
    private static string ItemFromModelName(string modelName)
    {
        var index = modelName.IndexOf(" w/ ", StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? modelName[(index + 4)..].Trim() : modelName;
    }
}
