using Deskling.Core.Death;
using Deskling.Core.Furniture;
using Deskling.Core.Stats;

namespace Deskling.Core.Tests.Death;

/// <summary>The neglect timers on their own: countdowns, pauses, resets and warnings.</summary>
public class MortalityTests
{
    private const double Critical = 15;
    private static readonly DeathRules Rules = new() { ZeroHours = 2, AllCriticalHours = 4 };

    /// <summary>Runs <paramref name="hours"/> in 1-minute updates at fixed stats; returns the cause of death, if any.</summary>
    private static DeathCause? Live(Mortality mortality, PetStats stats, double hours, List<PetWarning>? warnings = null, bool counting = true, DeathRules? rules = null)
    {
        warnings ??= [];
        for (var minute = 0; minute < hours * 60; minute++)
        {
            if (mortality.Update(stats, TimeSpan.FromMinutes(1), counting, rules ?? Rules, Critical, warnings) is { } cause)
            {
                return cause;
            }
        }

        return null;
    }

    [Fact]
    public void A_need_at_zero_kills_after_the_set_time()
    {
        var mortality = new Mortality();
        Assert.Null(Live(mortality, new PetStats(0, 50, 50), 1.9));
        Assert.Equal(DeathCause.Hunger, Live(mortality, new PetStats(0, 50, 50), 0.2));
    }

    [Theory]
    [InlineData(0, 50, 50, DeathCause.Hunger)]
    [InlineData(50, 0, 50, DeathCause.Sadness)]
    [InlineData(50, 50, 0, DeathCause.Exhaustion)]
    public void The_cause_is_the_need_that_ran_out(double hunger, double happiness, double energy, DeathCause cause) =>
        Assert.Equal(cause, Live(new Mortality(), new PetStats(hunger, happiness, energy), 2.1));

    [Fact]
    public void All_three_critical_kills_after_its_own_longer_time()
    {
        var mortality = new Mortality();
        Assert.Null(Live(mortality, new PetStats(10, 10, 10), 3.9));
        Assert.Equal(DeathCause.Neglect, Live(mortality, new PetStats(10, 10, 10), 0.2));
    }

    [Fact]
    public void Two_critical_needs_are_not_enough()
    {
        Assert.Null(Live(new Mortality(), new PetStats(10, 10, 40), 10));
    }

    [Fact]
    public void Rising_off_zero_resets_the_countdown()
    {
        var mortality = new Mortality();
        Live(mortality, new PetStats(0, 50, 50), 1.5);
        Live(mortality, new PetStats(1, 50, 50), 1.0 / 60);
        Assert.Equal(0, mortality.Timers.HungerAtZero);
        Assert.Null(Live(mortality, new PetStats(0, 50, 50), 1.5));
    }

    [Fact]
    public void Time_that_doesnt_count_pauses_the_countdown_without_resetting_it()
    {
        var mortality = new Mortality();
        Live(mortality, new PetStats(0, 50, 50), 1.5);
        Assert.Null(Live(mortality, new PetStats(0, 50, 50), 24, counting: false)); // away, or in a pen
        Assert.Equal(1.5 * 3600, mortality.Timers.HungerAtZero, 1);
        Assert.Equal(DeathCause.Hunger, Live(mortality, new PetStats(0, 50, 50), 0.6));
    }

    [Fact]
    public void Saved_timers_carry_on()
    {
        var mortality = new Mortality(new NeglectTimers { HappinessAtZero = 1.9 * 3600 });
        Assert.Equal(DeathCause.Sadness, Live(mortality, new PetStats(50, 0, 50), 0.2));
    }

    [Fact]
    public void Switched_off_nothing_counts_and_nothing_warns()
    {
        var mortality = new Mortality(new NeglectTimers { HungerAtZero = 3600 });
        var warnings = new List<PetWarning>();
        Assert.Null(Live(mortality, new PetStats(0, 0, 0), 10, warnings, rules: Rules with { Enabled = false }));
        Assert.True(mortality.Timers.IsZero);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Going_critical_warns_once_until_the_need_recovers()
    {
        var mortality = new Mortality();
        var warnings = new List<PetWarning>();
        Live(mortality, new PetStats(14, 50, 50), 1, warnings);
        Assert.Equal([new PetWarning(WarningKind.Critical, Need.Hunger)], warnings);

        Live(mortality, new PetStats(17, 50, 50), 0.1, warnings); // back over critical, but only just
        Live(mortality, new PetStats(14, 50, 50), 0.1, warnings);
        Assert.Single(warnings);

        Live(mortality, new PetStats(15 + Mortality.Hysteresis, 50, 50), 0.1, warnings);
        Live(mortality, new PetStats(14, 50, 50), 0.1, warnings);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Reaching_zero_warns_with_the_time_left_and_doesnt_also_warn_critical()
    {
        var mortality = new Mortality();
        var warnings = new List<PetWarning>();
        Live(mortality, new PetStats(50, 50, 0), 0.5, warnings);
        var warning = Assert.Single(warnings);
        Assert.Equal(WarningKind.AtZero, warning.Kind);
        Assert.Equal(Need.Energy, warning.Need);
        Assert.Equal(2, warning.HoursLeft, 1);
    }

    [Fact]
    public void Critical_then_zero_warns_twice()
    {
        var mortality = new Mortality();
        var warnings = new List<PetWarning>();
        Live(mortality, new PetStats(10, 50, 50), 0.1, warnings);
        Live(mortality, new PetStats(0, 50, 50), 0.1, warnings);
        Assert.Equal([WarningKind.Critical, WarningKind.AtZero], warnings.Select(w => w.Kind));
    }

    [Fact]
    public void All_critical_warns_once()
    {
        var mortality = new Mortality();
        var warnings = new List<PetWarning>();
        Live(mortality, new PetStats(10, 10, 10), 1, warnings);
        Assert.Equal(4, warnings.Count); // each need, then all three
        Assert.Equal(WarningKind.AllCritical, warnings[^1].Kind);
        Assert.Equal(Need.None, warnings[^1].Need);
        Assert.Equal(4, warnings[^1].HoursLeft, 1);
    }

    [Fact]
    public void No_warnings_while_the_time_doesnt_count_they_come_once_it_does()
    {
        var mortality = new Mortality();
        var warnings = new List<PetWarning>();
        Live(mortality, new PetStats(0, 50, 50), 1, warnings, counting: false);
        Assert.Empty(warnings);
        Live(mortality, new PetStats(0, 50, 50), 1.0 / 60, warnings);
        Assert.Equal(WarningKind.AtZero, Assert.Single(warnings).Kind);
    }
}
