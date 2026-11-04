using DesktopPet.Core.Behavior;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Stats;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.Traits;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Traits;

public class PetTraitsTests
{
    private static readonly Core.World.DesktopSnapshot Desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot();

    [Fact]
    public void Neutral_traits_leave_the_settings_unchanged()
    {
        Assert.Equal(PetConfig.Default, PetTraits.Neutral("Ava").ApplyTo(PetConfig.Default));
    }

    [Fact]
    public void Neediness_scales_decay_but_not_sleep()
    {
        var traits = new PetTraits { Neediness = new Neediness(Hunger: 2, Happiness: 0.5, Energy: 1.5) };
        var s = traits.ApplyTo(PetConfig.Default).Stats;
        var d = StatRules.Default;

        Assert.Equal(d.HungerPerHour * 2, s.HungerPerHour);
        Assert.Equal(d.HappinessPerHour * 0.5, s.HappinessPerHour);
        Assert.Equal(d.EnergyPerHour * 1.5, s.EnergyPerHour);
        Assert.Equal(d.ActiveEnergyPerHour * 1.5, s.ActiveEnergyPerHour);
        Assert.Equal(d.SleepEnergyPerHour, s.SleepEnergyPerHour);
        Assert.Equal(d.FeedHunger, s.FeedHunger); // care works the same on every pet
    }

    [Fact]
    public void Behavior_scales_weights_and_wall_chances()
    {
        var traits = new PetTraits { Behavior = new BehaviorTraits(Idle: 0.5, Walk: 2, Sit: 3, Sleep: 0, Climb: 2, Push: 0.5) };
        var c = traits.ApplyTo(PetConfig.Default);
        var d = PetConfig.Default;

        Assert.Equal(new BehaviorWeights(d.Weights.Idle * 0.5, d.Weights.Walk * 2, d.Weights.Sit * 3, 0), c.Weights);
        Assert.Equal(d.ClimbChance * 2, c.ClimbChance, 9);
        Assert.Equal(d.PushChance * 0.5, c.PushChance, 9);
    }

    [Fact]
    public void Wall_chances_are_scaled_down_together_when_they_would_exceed_one()
    {
        var shared = PetConfig.Default with { ClimbChance = 0.4, PushChance = 0.4 };
        var c = new PetTraits { Behavior = new BehaviorTraits(Climb: 3, Push: 2) }.ApplyTo(shared);

        Assert.Equal(1, c.ClimbChance + c.PushChance, 9);
        Assert.Equal(1.2 / 2.0, c.ClimbChance, 9); // 1.2 : 0.8 kept
    }

    [Fact]
    public void Applied_settings_stay_valid_for_any_valid_traits()
    {
        var extreme = new PetTraits
        {
            Neediness = new Neediness(10, 10, 10),
            Behavior = new BehaviorTraits(10, 10, 10, 10, 10, 10),
        };
        Assert.Empty(PetConfigValidator.Validate(extreme.ApplyTo(PetConfig.Default)));
    }

    [Fact]
    public void A_needier_pet_gets_hungry_faster()
    {
        var clock = new FakeClock();
        var average = NewSim(Desktop, clock: clock);
        var needy = new PetSimulation(PetConfig.Default, Desktop, new FakeRandom(0.0), clock,
            traits: new PetTraits { Name = "Hugo", Neediness = new Neediness(Hunger: 2) });

        clock.Advance(TimeSpan.FromHours(1));
        average.Tick(Dt, Desktop);
        needy.Tick(Dt, Desktop);

        var start = PetStats.Fresh.Hunger;
        Assert.Equal(2 * (start - average.Stats.Hunger), start - needy.Stats.Hunger, 6);
        Assert.Equal("Hugo", needy.Traits.Name);
    }

    [Fact]
    public void Edited_traits_apply_from_now_on()
    {
        // Gaps under a minute: longer ones count as the PC sleeping, which halves the decay.
        var gap = TimeSpan.FromSeconds(30);
        var perGap = StatRules.Default.HungerPerHour * gap.TotalHours;
        var clock = new FakeClock();
        var sim = NewSim(Desktop, clock: clock);
        clock.Advance(gap);
        sim.SetTraits(new PetTraits { Name = "Ivy", Neediness = new Neediness(Hunger: 3) }); // the first gap was average
        var afterFirstGap = sim.Stats.Hunger;
        clock.Advance(gap);
        sim.Tick(Dt, Desktop);

        Assert.Equal(PetStats.Fresh.Hunger + perGap, afterFirstGap, 6);
        Assert.Equal(afterFirstGap + (3 * perGap), sim.Stats.Hunger, 6);
        Assert.Equal("Ivy", sim.Traits.Name);
    }

    [Fact]
    public void Traits_survive_a_settings_reload()
    {
        var sim = NewSim(Desktop);
        sim.SetTraits(new PetTraits { Behavior = new BehaviorTraits(Walk: 2) });
        sim.Reconfigure(PetConfig.Default with { Weights = new BehaviorWeights(Walk: 10) });

        Assert.Equal(20, sim.Config.Weights.Walk);
    }

    [Fact]
    public void A_lazy_pet_sits_more_than_an_adventurous_one()
    {
        var lazy = new PetTraits { Behavior = new BehaviorTraits(Walk: 0.5, Sit: 2) }.ApplyTo(PetConfig.Default);
        var keen = new PetTraits { Behavior = new BehaviorTraits(Walk: 2, Sit: 0.5) }.ApplyTo(PetConfig.Default);
        var healthy = PetStats.Fresh;

        var (_, lazyWalk, lazySit, _) = BehaviorPicker.Weights(lazy, healthy);
        var (_, keenWalk, keenSit, _) = BehaviorPicker.Weights(keen, healthy);
        Assert.True(lazySit > keenSit);
        Assert.True(lazyWalk < keenWalk);
    }

    // ---- Rolling a new pet ----

    [Fact]
    public void Multipliers_are_log_uniform_in_the_range()
    {
        var range = new TraitRange(0.5, 2.0);
        Assert.Equal(0.5, TraitRoller.Multiplier(new FakeRandom(0.0), range));
        Assert.Equal(1.0, TraitRoller.Multiplier(new FakeRandom(0.5), range)); // the median is average
        Assert.Equal(2.0, TraitRoller.Multiplier(new FakeRandom(0.99999999), range));
    }

    [Fact]
    public void A_fixed_range_gives_that_value()
    {
        Assert.Equal(1.0, TraitRoller.Multiplier(new FakeRandom(0.3), new TraitRange(1, 1)));
    }

    [Fact]
    public void Rolled_traits_are_valid_and_in_range()
    {
        var config = PetConfig.Default with { NewPetNeediness = new TraitRange(0.8, 1.25) };
        var random = new Abstractions.SeededRandom(7);
        for (var i = 0; i < 50; i++)
        {
            var t = TraitRoller.Roll(config, random, []);
            Assert.Empty(PetTraitsFile.Validate(t));
            Assert.InRange(t.Neediness.Hunger, 0.8, 1.25);
            Assert.InRange(t.Behavior.Climb, 0.5, 2.0);
            Assert.InRange(t.Chat.Banter, 0.5, 2.0);
        }
    }

    [Fact]
    public void New_names_avoid_taken_ones_ignoring_case()
    {
        var taken = PetNames.All.Skip(1).Select(n => n.ToUpperInvariant()).ToList();
        Assert.Equal(PetNames.All[0], PetNames.Pick(new FakeRandom(0.7), taken));
    }

    [Fact]
    public void Names_get_a_number_once_all_are_taken()
    {
        var taken = PetNames.All.Append($"{PetNames.All[0]} 2");
        Assert.Equal($"{PetNames.All[0]} 3", PetNames.Pick(new FakeRandom(0.0), taken));
    }

    [Fact]
    public void Name_list_has_200_distinct_valid_names()
    {
        Assert.Equal(100, PetNames.Popular.Count);
        Assert.Equal(100, PetNames.Rare.Count);
        Assert.Equal(200, PetNames.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(PetNames.All, n => Assert.Empty(PetTraitsFile.Validate(PetTraits.Neutral(n))));
    }

    // ---- traits.json ----

    [Fact]
    public void Traits_file_round_trips()
    {
        var traits = new PetTraits
        {
            Name = "Barnaby",
            Neediness = new Neediness(1.25, 0.6, 2),
            Behavior = new BehaviorTraits(1, 0.5, 1.5, 0.7, 1.9, 0.55),
            Chat = new ChatTraits(0.5, 2, 0, 1.3, 1.7),
        };
        var parsed = PetTraitsFile.Parse(PetTraitsFile.Serialize(traits));

        Assert.Empty(parsed.Errors);
        Assert.Equal(traits, parsed.Traits);
    }

    [Fact]
    public void Traits_file_is_forgiving_about_form()
    {
        const string text = """
            // hand-written
            { "name": "  Wilbur ", "behavior": { "climb": 3, }, }
            """;
        var parsed = PetTraitsFile.Parse(text);

        Assert.Empty(parsed.Errors);
        Assert.Equal("Wilbur", parsed.Traits!.Name);
        Assert.Equal(new Neediness(), parsed.Traits.Neediness);
        Assert.Equal(new BehaviorTraits(Climb: 3), parsed.Traits.Behavior);
        Assert.Equal(new ChatTraits(), parsed.Traits.Chat); // older files have no Chat section
    }

    [Fact]
    public void Chat_sections_from_before_curiosity_count_it_as_average()
    {
        var parsed = PetTraitsFile.Parse("""{ "Name": "Ava", "Chat": { "Needy": 1.2, "Banter": 0.8, "Jokes": 1.5, "Notes": 0.6 } }""");

        Assert.Equal(new ChatTraits(1.2, 0.8, 1.5, 0.6, Curiosity: 1), parsed.Traits!.Chat);
    }

    [Fact]
    public void Traits_file_with_a_null_section_uses_averages()
    {
        var parsed = PetTraitsFile.Parse("""{ "Name": "Edna", "Neediness": null }""");
        Assert.Equal(new Neediness(), parsed.Traits!.Neediness);
    }

    [Theory]
    [InlineData("""{ "Name": "" }""", "Name is missing.")]
    [InlineData("""{ "Behavior": { "Walk": 0 } }""", "Name is missing.")]
    [InlineData("""{ "Name": "A name far too long for a menu header" }""", "Name must be one line of at most 24 characters.")]
    [InlineData("""{ "Name": "Ava", "Neediness": { "Hunger": 11 } }""", "Neediness.Hunger must be between 0 and 10 (it is 11).")]
    [InlineData("""{ "Name": "Ava", "Behavior": { "Push": -1 } }""", "Behavior.Push must be between 0 and 10 (it is -1).")]
    [InlineData("""{ "Name": "Ava", "Behavior": { "Idle": 0, "Walk": 0, "Sit": 0 } }""", "Behavior.Idle, Behavior.Walk and Behavior.Sit can't all be 0.")]
    [InlineData("""{ "Name": "Ava", "Chat": { "Jokes": 12 } }""", "Chat.Jokes must be between 0 and 10 (it is 12).")]
    [InlineData("""{ "Name": "Ava", "Chat": { "Curiosity": -2 } }""", "Chat.Curiosity must be between 0 and 10 (it is -2).")]
    [InlineData("{ nope", "Not valid JSON (line 1).")]
    [InlineData("null", "The file is empty.")]
    public void Traits_file_problems_are_reported(string text, string error)
    {
        var parsed = PetTraitsFile.Parse(text);
        Assert.Null(parsed.Traits);
        Assert.Contains(error, parsed.Errors);
    }

    // ---- Personality ----

    [Fact]
    public void Default_personality_names_the_strongest_traits()
    {
        var traits = new PetTraits
        {
            Name = "Rosie",
            Neediness = new Neediness(Hunger: 1.9, Energy: 0.6),
            Behavior = new BehaviorTraits(Climb: 1.5, Walk: 1.2, Push: 1.45),
        };

        Assert.Equal(
            "Rosie is a little grey rabbit who is always hungry, has boundless energy and loves climbing.",
            Personality.Describe(traits));
    }

    [Fact]
    public void An_average_pet_is_easygoing()
    {
        Assert.Equal("Ava is a little grey rabbit who is pretty easygoing.", Personality.Describe(PetTraits.Neutral("Ava")));
    }

    [Fact]
    public void Personality_text_is_trimmed_and_capped()
    {
        Assert.Equal("Hi.", Personality.Clean("  Hi.\r\n"));
        Assert.Equal(Personality.MaxLength, Personality.Clean(new string('a', 5000)).Length);
    }
}
