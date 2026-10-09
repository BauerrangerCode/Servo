namespace Servitor.App.BattleTracker.ActionQueue;

/// <summary>One named alternative of a rule that makes the player pick one (e.g. a Combat Doctrine).</summary>
public sealed record RuleOption(string Name, string Text);
