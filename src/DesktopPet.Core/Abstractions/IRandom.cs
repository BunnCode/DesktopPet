namespace DesktopPet.Core.Abstractions;

/// <summary>Randomness source, injectable so simulations are deterministic in tests.</summary>
public interface IRandom
{
    /// <summary>Uniform in [0, 1).</summary>
    double NextDouble();
}

public static class RandomExtensions
{
    /// <summary>Uniform in [min, max).</summary>
    public static double Range(this IRandom random, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(random);
        return min + (random.NextDouble() * (max - min));
    }

    public static bool Chance(this IRandom random, double probability)
    {
        ArgumentNullException.ThrowIfNull(random);
        return random.NextDouble() < probability;
    }
}

/// <summary><see cref="IRandom"/> over <see cref="System.Random"/>; same seed, same pet.</summary>
public sealed class SeededRandom(int seed) : IRandom
{
    // Not used for security; determinism per seed is the point.
#pragma warning disable CA5394
    private readonly Random _random = new(seed);

    public double NextDouble() => _random.NextDouble();
#pragma warning restore CA5394
}
