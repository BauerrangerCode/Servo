namespace Servitor.App.RulesData.Domain;

/// <summary>
/// <paramref name="DescriptionHtml"/> is sanitized markup (<see cref="Shared.Html.HtmlSanitizer"/>), safe to
/// render via MarkupString. <paramref name="Detachment"/> is empty for Core Stratagems.
/// </summary>
public sealed record Stratagem(
    string Id,
    string Name,
    StratagemCategory Category,
    int? CpCost,
    string Legend,
    string Turn,
    string Phase,
    string DescriptionHtml,
    string Detachment);
