using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Tests.Stats;

public class StatsSystemTests
{
    private static readonly StatRules Rules = StatRules.Default;
    private static readonly PetStats Full = new(100, 100, 100);

    [Fact]
    public void Decays_at_hourly_rates_while_resting()
    {
        var s = StatsSystem.Advance(Full, TimeSpan.FromHours(2), StatActivity.Resting, away: false, Rules);
        Assert.Equal(100 + (2 * Rules.HungerPerHour), s.Hunger, 6);
        Assert.Equal(100 + (2 * Rules.HappinessPerHour), s.Happiness, 6);
        Assert.Equal(100 + (2 * Rules.EnergyPerHour), s.Energy, 6);
    }

    [Fact]
    public void Being_active_burns_extra_energy()
    {
        var resting = StatsSystem.Advance(Full, TimeSpan.FromHours(1), StatActivity.Resting, false, Rules);
        var active = StatsSystem.Advance(Full, TimeSpan.FromHours(1), StatActivity.Active, false, Rules);
        Assert.Equal(resting.Energy + Rules.ActiveEnergyPerHour, active.Energy, 6);
        Assert.Equal(resting.Hunger, active.Hunger);
    }

    [Fact]
    public void Sleeping_restores_energy_and_slows_other_decay()
    {
        var s = StatsSystem.Advance(new PetStats(80, 80, 20), TimeSpan.FromHours(1), StatActivity.Sleeping, false, Rules);
        Assert.Equal(20 + Rules.SleepEnergyPerHour, s.Energy, 6);
        Assert.Equal(80 + (Rules.HungerPerHour * Rules.SleepDecayFactor), s.Hunger, 6);
    }

    [Fact]
    public void Stays_within_0_and_100()
    {
        var starving = StatsSystem.Advance(new PetStats(5, 5, 5), TimeSpan.FromDays(10), StatActivity.Resting, false, Rules);
        Assert.Equal(new PetStats(0, 0, 0), starving);

        var rested = StatsSystem.Advance(new PetStats(50, 50, 90), TimeSpan.FromHours(5), StatActivity.Sleeping, false, Rules);
        Assert.Equal(100, rested.Energy);
    }

    [Fact]
    public void Present_user_can_let_stats_reach_critical()
    {
        var s = StatsSystem.Advance(new PetStats(30, 30, 30), TimeSpan.FromHours(5), StatActivity.Resting, away: false, Rules);
        Assert.True(s.Hunger < Rules.Critical);
    }

    // ---- The rule: nothing reaches critical while the machine is locked or off ----

    [Fact]
    public void Away_decay_stops_at_the_floor_just_above_critical()
    {
        var s = StatsSystem.Advance(new PetStats(60, 60, 60), TimeSpan.FromDays(3), StatActivity.Resting, away: true, Rules);

        Assert.Equal(new PetStats(Rules.AwayFloor, Rules.AwayFloor, Rules.AwayFloor), s);
        Assert.True(Rules.AwayFloor > Rules.Critical);
    }

    [Fact]
    public void Away_never_lowers_a_stat_already_below_the_floor()
    {
        var s = StatsSystem.Advance(new PetStats(12, 50, 3), TimeSpan.FromDays(1), StatActivity.Resting, away: true, Rules);
        Assert.Equal(12, s.Hunger);
        Assert.Equal(Rules.AwayFloor, s.Happiness);
        Assert.Equal(3, s.Energy);
    }

    [Fact]
    public void Away_still_allows_stats_to_rise()
    {
        var s = StatsSystem.Advance(new PetStats(50, 50, 10), TimeSpan.FromHours(1), StatActivity.Sleeping, away: true, Rules);
        Assert.Equal(10 + Rules.SleepEnergyPerHour, s.Energy, 6);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(24 * 7)]
    [InlineData(24 * 365)]
    public void Offline_catch_up_never_reaches_critical(int hours)
    {
        var s = StatsSystem.CatchUp(new PetStats(70, 70, 70), TimeSpan.FromHours(hours), Rules);
        Assert.True(s.Lowest > Rules.Critical, $"{s} after {hours} h");
    }

    [Fact]
    public void Offline_counts_as_a_nap()
    {
        var s = StatsSystem.CatchUp(new PetStats(70, 70, 25), TimeSpan.FromHours(8), Rules);
        Assert.Equal(100, s.Energy);
    }

    [Fact]
    public void Negative_or_zero_elapsed_changes_nothing()
    {
        var start = new PetStats(50, 50, 50);
        Assert.Equal(start, StatsSystem.Advance(start, TimeSpan.Zero, StatActivity.Active, false, Rules));
        Assert.Equal(start, StatsSystem.CatchUp(start, TimeSpan.FromHours(-3), Rules)); // clock went backwards
    }

    // ---- Actions ----

    [Fact]
    public void Feeding_fills_hunger_and_cheers_up_a_little()
    {
        var s = StatsSystem.Apply(new PetStats(40, 50, 50), PetAction.Feed, Rules);
        Assert.Equal(40 + Rules.FeedHunger, s.Hunger);
        Assert.Equal(50 + Rules.FeedHappiness, s.Happiness);
    }

    [Fact]
    public void Playing_trades_energy_and_food_for_happiness()
    {
        var s = StatsSystem.Apply(new PetStats(50, 50, 50), PetAction.Play, Rules);
        Assert.Equal(50 + Rules.PlayHappiness, s.Happiness);
        Assert.Equal(50 + Rules.PlayEnergy, s.Energy);
        Assert.Equal(50 + Rules.PlayHunger, s.Hunger);
    }

    [Fact]
    public void Actions_clamp_at_100()
    {
        Assert.Equal(100, StatsSystem.Apply(new PetStats(90, 99, 50), PetAction.Feed, Rules).Hunger);
        Assert.Equal(100, StatsSystem.Petted(new PetStats(50, 99, 50), Rules).Happiness);
    }
}
