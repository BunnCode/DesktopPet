using DesktopPet.Core.Stats;
using DesktopPet.Core.Traits;

namespace DesktopPet.App.UI;

/// <summary>What the menu needs from the pet it was opened for.</summary>
internal interface IPetControls
{
    string Name { get; }

    /// <summary>Name and multipliers (read-only here; edited in the pet's traits.json).</summary>
    PetTraits Traits { get; }

    /// <summary>The pet's personality text.</summary>
    string Personality { get; }

    PetStats Stats { get; }

    bool IsAsleep { get; }

    /// <summary>Current stat thresholds (they follow live settings).</summary>
    StatRules Rules { get; }

    void Apply(PetAction action);
}
