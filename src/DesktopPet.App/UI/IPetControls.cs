using DesktopPet.Core.Stats;

namespace DesktopPet.App.UI;

/// <summary>What the menu needs from the pet it was opened for.</summary>
internal interface IPetControls
{
    string Name { get; }

    PetStats Stats { get; }

    bool IsAsleep { get; }

    /// <summary>Current stat thresholds (they follow live settings).</summary>
    StatRules Rules { get; }

    void Apply(PetAction action);
}
