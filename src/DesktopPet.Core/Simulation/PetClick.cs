namespace DesktopPet.Core.Simulation;

/// <summary>What a click (a press released without dragging) did to the pet.</summary>
public enum PetClick
{
    /// <summary>It was standing: it was petted (happier, a happy hop).</summary>
    Petted,

    /// <summary>It was asleep: it woke up.</summary>
    WokenUp,

    /// <summary>It was falling, climbing or hanging on: nothing happened, but it felt it.</summary>
    Poked,
}
