namespace Deskling.Core.Simulation;

/// <summary>
/// Fixed-timestep accumulator. Feed it variable frame times; it says how many fixed steps to run
/// and the leftover fraction (<see cref="Alpha"/>) for render interpolation.
/// </summary>
public sealed class FixedStepper
{
    private const double Epsilon = 1e-9;
    private double _accumulator;

    public FixedStepper(double stepSeconds, int maxStepsPerAdvance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStepsPerAdvance, 1);
        StepSeconds = stepSeconds;
        MaxStepsPerAdvance = maxStepsPerAdvance;
    }

    public double StepSeconds { get; }

    /// <summary>Cap per advance. Time beyond the cap is dropped (avoids a spiral of death after sleep/resume or a debugger break).</summary>
    public int MaxStepsPerAdvance { get; }

    /// <summary>Fraction of a step left in the accumulator, in [0, 1).</summary>
    public double Alpha => _accumulator / StepSeconds;

    /// <summary>Adds elapsed real time and returns the number of fixed steps to simulate now.</summary>
    public int Advance(double elapsedSeconds)
    {
        if (!(elapsedSeconds > 0) || double.IsInfinity(elapsedSeconds))
        {
            return 0;
        }

        _accumulator += elapsedSeconds;
        var steps = (int)System.Math.Floor((_accumulator + Epsilon) / StepSeconds);

        if (steps > MaxStepsPerAdvance)
        {
            _accumulator = 0;
            return MaxStepsPerAdvance;
        }

        _accumulator = System.Math.Max(0, _accumulator - (steps * StepSeconds));
        return steps;
    }

    public void Reset() => _accumulator = 0;
}
