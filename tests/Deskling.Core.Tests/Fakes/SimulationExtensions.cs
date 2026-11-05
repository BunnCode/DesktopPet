using Deskling.Core.Abstractions;
using Deskling.Core.Physics;
using Deskling.Core.Simulation;
using Deskling.Core.World;

namespace Deskling.Core.Tests.Fakes;

internal static class SimulationExtensions
{
    public const double Dt = 1.0 / 60.0;

    /// <summary>
    /// A simulation with deterministic randomness. The default (always 0.0) picks Idle with its minimum duration
    /// and never turns, so tests that don't care about behavior get a pet that stays put.
    /// </summary>
    /// <remarks>The wall clock is frozen unless a <paramref name="clock"/> is passed, so stats don't drift with real time.</remarks>
    public static PetSimulation NewSim(
        DesktopSnapshot snapshot, IRandom? random = null, PetConfig? config = null, FakeClock? clock = null,
        Core.Persistence.SaveData? save = null, Core.Furniture.FurnitureWorld? furniture = null) =>
        new(config ?? PetConfig.Default, snapshot, random ?? new FakeRandom(0.0), clock ?? new FakeClock(), save, furniture: furniture);

    public static bool Grounded(PetSimulation sim) => sim.Mode == BodyMode.Grounded;

    /// <summary>Steps until <paramref name="until"/> holds; fails the test if it doesn't within <paramref name="maxTicks"/>.</summary>
    public static int RunUntil(this PetSimulation sim, DesktopSnapshot snapshot, Func<PetSimulation, bool> until, int maxTicks = 600)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            sim.Tick(Dt, snapshot);
            if (until(sim))
            {
                return tick;
            }
        }

        Assert.Fail($"Condition not met within {maxTicks} ticks. Feet={sim.Current.FeetPos}, Mode={sim.Mode}, State={sim.State}");
        return -1;
    }

    public static void Run(this PetSimulation sim, DesktopSnapshot snapshot, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Tick(Dt, snapshot);
        }
    }

    public static void RunSeconds(this PetSimulation sim, DesktopSnapshot snapshot, double seconds) =>
        sim.Run(snapshot, (int)System.Math.Round(seconds / Dt));
}
