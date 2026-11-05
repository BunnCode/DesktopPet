using Deskling.Core.Simulation;

namespace Deskling.Core.Tests.Simulation;

public class FixedStepperTests
{
    private const double Step = 1.0 / 60.0;

    [Fact]
    public void Less_than_a_step_runs_nothing_and_accumulates()
    {
        var s = new FixedStepper(Step, 5);
        Assert.Equal(0, s.Advance(Step * 0.5));
        Assert.Equal(0.5, s.Alpha, 6);
        Assert.Equal(1, s.Advance(Step * 0.5));
        Assert.Equal(0, s.Alpha, 6);
    }

    [Fact]
    public void Exact_multiple_of_step_runs_that_many_despite_float_error()
    {
        var s = new FixedStepper(Step, 10);
        Assert.Equal(3, s.Advance(3 * Step));
        Assert.True(s.Alpha < 1e-6);
    }

    [Fact]
    public void Steady_60hz_frames_yield_one_step_each()
    {
        var s = new FixedStepper(Step, 5);
        var total = 0;
        for (var i = 0; i < 600; i++)
        {
            total += s.Advance(Step);
        }

        Assert.Equal(600, total);
    }

    [Fact]
    public void Steady_144hz_frames_average_to_60_steps_per_second()
    {
        var s = new FixedStepper(Step, 5);
        var total = 0;
        for (var i = 0; i < 144 * 10; i++)
        {
            total += s.Advance(1.0 / 144.0);
        }

        Assert.InRange(total, 599, 600);
    }

    [Fact]
    public void Huge_gap_is_capped_and_backlog_dropped()
    {
        var s = new FixedStepper(Step, 5);
        Assert.Equal(5, s.Advance(10.0)); // e.g. resume from sleep
        Assert.Equal(0, s.Alpha);
        Assert.Equal(1, s.Advance(Step)); // back to normal immediately
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_elapsed_is_ignored(double elapsed)
    {
        var s = new FixedStepper(Step, 5);
        Assert.Equal(0, s.Advance(elapsed));
        Assert.Equal(0, s.Alpha);
    }

    [Fact]
    public void Rejects_bad_configuration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepper(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepper(Step, 0));
    }
}
