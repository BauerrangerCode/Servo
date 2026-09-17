namespace Servitor.App.RulesData.Domain;

/// <summary>
/// <paramref name="IsUpgrade"/> mirrors the source's own "upgrade" flag (up to 3 copies allowed, may go on
/// non-Character units) as confirmed structurally in BSData's constraints (see docs/Open-Questions-Catalog.md).
/// </summary>
public sealed record Enhancement(
    string Id,
    string Name,
    string Legend,
    string DescriptionHtml,
    int Cost,
    string Detachment,
    bool IsUpgrade,
    string? SupportLeaderHtml);
