namespace Servitor.RulesData.Domain;

/// <summary>
/// All text fields are BSData-style Markdown (<c>**KEYWORD**</c>, <c>- </c> lists). The rules-text source's
/// WHEN/TARGET/EFFECT/RESTRICTIONS blocks are split into their own fields; every other labelled block (e.g.
/// "ELIGIBLE IF") is kept in <paramref name="AdditionalText"/> so nothing is dropped. <paramref name="Detachment"/>
/// is empty for Core Stratagems. A stratagem always has a CP cost (<paramref name="CpCost"/>); cost-free
/// entries of the rules-text source are actions and become <see cref="CoreRule"/>s.
/// </summary>
public sealed record Stratagem(
    string Id,
    string Name,
    StratagemCategory Category,
    int? CpCost,
    string Legend,
    string Turn,
    string Phase,
    string When,
    string Target,
    string Effect,
    string Restrictions,
    string AdditionalText,
    string Detachment,
    GameMode Mode = GameMode.Standard);
