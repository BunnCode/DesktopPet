using Deskling.Core.Abstractions;

namespace Deskling.Core.Tests.Fakes;

/// <summary>Returns <paramref name="script"/> in order, then <paramref name="fallback"/> forever.</summary>
internal sealed class ScriptedRandom(double fallback, params double[] script) : IRandom
{
    private int _next;

    public double NextDouble() => _next < script.Length ? script[_next++] : fallback;
}
