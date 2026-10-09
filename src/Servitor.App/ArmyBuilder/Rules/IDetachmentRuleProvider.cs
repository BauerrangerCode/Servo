namespace Servitor.App.ArmyBuilder.Rules;

/// <summary>Holds the detachment rules of the rules file for as long as the app runs.</summary>
public interface IDetachmentRuleProvider
{
    /// <summary>The rules loaded so far; empty until <see cref="LoadAsync"/> has run.</summary>
    DetachmentRuleSet Current { get; }

    /// <summary>Reads the rules file again, so a changed file is picked up without restarting the app.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
