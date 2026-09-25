using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servitor.RulesData.Mapping.BsData;

public sealed class BsCategoryLink
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public sealed class BsConstraint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// Raw JSON value. Almost always an integer for a "max"/"min" constraint, but the schema is shared with
    /// non-numeric constraint kinds elsewhere in the source, so it is read defensively via <see cref="BsValue"/>.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

public sealed class BsCost
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public sealed class BsInfoLink
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";
}

public sealed class BsModifier
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>Raw JSON value — modifiers also set boolean fields (e.g. "hidden"), not only numeric ones.</summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("conditions")]
    public List<BsCondition> Conditions { get; set; } = [];

    /// <summary>Nested and/or groups of conditions; a modifier that has any is not interpreted.</summary>
    [JsonPropertyName("conditionGroups")]
    public List<JsonElement> ConditionGroups { get; set; } = [];
}

public sealed class BsCondition
{
    [JsonPropertyName("childName")]
    public string? ChildName { get; set; }

    [JsonPropertyName("childId")]
    public string? ChildId { get; set; }

    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}

public static class BsValue
{
    public static int? AsInt(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;
}
