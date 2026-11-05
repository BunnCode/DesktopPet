using Deskling.Core.Math;

namespace Deskling.Core.Input;

/// <summary>
/// Estimates release velocity from recent pointer samples with a least-squares line fit per axis,
/// which is far less jittery than "last delta / last dt".
/// </summary>
public sealed class ThrowEstimator
{
    private readonly List<(double Time, Vec2 Position)> _samples = [];

    public ThrowEstimator(double windowSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);
        WindowSeconds = windowSeconds;
    }

    /// <summary>Only samples this recent (relative to the query time) count.</summary>
    public double WindowSeconds { get; }

    public void Clear() => _samples.Clear();

    public void Add(double time, Vec2 position)
    {
        _samples.Add((time, position));

        // Keep memory bounded; anything older than a couple of windows can't matter.
        var cutoff = time - (WindowSeconds * 2);
        var stale = 0;
        while (stale < _samples.Count && _samples[stale].Time < cutoff)
        {
            stale++;
        }

        if (stale > 0)
        {
            _samples.RemoveRange(0, stale);
        }
    }

    /// <summary>Velocity in px/s at <paramref name="now"/>; zero if there isn't enough recent motion data.</summary>
    public Vec2 Velocity(double now)
    {
        if (_samples.Count < 2)
        {
            return Vec2.Zero;
        }

        var from = now - WindowSeconds;
        var origin = _samples[^1].Position; // work relative to recent values for numerical stability
        double n = 0, sumT = 0, sumX = 0, sumY = 0, sumTT = 0, sumTX = 0, sumTY = 0;

        foreach (var (time, position) in _samples)
        {
            if (time < from || time > now)
            {
                continue;
            }

            var t = time - now;
            var p = position - origin;
            n++;
            sumT += t;
            sumX += p.X;
            sumY += p.Y;
            sumTT += t * t;
            sumTX += t * p.X;
            sumTY += t * p.Y;
        }

        var denominator = (n * sumTT) - (sumT * sumT);
        if (n < 2 || denominator <= 1e-12)
        {
            return Vec2.Zero;
        }

        return new Vec2(
            SnapToZero(((n * sumTX) - (sumT * sumX)) / denominator),
            SnapToZero(((n * sumTY) - (sumT * sumY)) / denominator));
    }

    /// <summary>Rounding residue (≈1e-12 px/s) from a stationary pointer must read as exactly zero, e.g. for facing.</summary>
    private static double SnapToZero(double v) => System.Math.Abs(v) < 1e-6 ? 0 : v;
}
