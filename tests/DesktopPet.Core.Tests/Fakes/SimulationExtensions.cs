using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;

namespace DesktopPet.Core.Tests.Fakes;

internal static class SimulationExtensions
{
    public const double Dt = 1.0 / 60.0;

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

        Assert.Fail($"Condition not met within {maxTicks} ticks. Feet={sim.Current.FeetPos}, Mode={sim.Mode}");
        return -1;
    }

    public static void Run(this PetSimulation sim, DesktopSnapshot snapshot, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Tick(Dt, snapshot);
        }
    }
}
