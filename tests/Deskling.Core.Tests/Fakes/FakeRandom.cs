using Deskling.Core.Abstractions;

namespace Deskling.Core.Tests.Fakes;

/// <summary>Returns the given values in order, cycling. <c>new FakeRandom(0.0)</c> always returns 0.</summary>
internal sealed class FakeRandom(params double[] values) : IRandom
{
    private int _next;

    public int Calls => _next;

    public double NextDouble() => values[_next++ % values.Length];
}
