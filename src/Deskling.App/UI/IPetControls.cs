using Deskling.Core.Stats;
using Deskling.Core.Traits;

namespace Deskling.App.UI;

/// <summary>What the menu needs from the pet it was opened for.</summary>
internal interface IPetControls
{
    string Name { get; }

    /// <summary>Name and multipliers (read-only here; edited in the Traits of the pet's save.json).</summary>
    PetTraits Traits { get; }

    /// <summary>The pet's personality text.</summary>
    string Personality { get; }

    PetStats Stats { get; }

    bool IsAsleep { get; }

    /// <summary>Current stat thresholds (they follow live settings).</summary>
    StatRules Rules { get; }

    void Apply(PetAction action);
}
