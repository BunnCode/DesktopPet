using DesktopPet.Core.Breeding;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Math;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Tests.Fakes;
using DesktopPet.Core.Traits;
using static DesktopPet.Core.Tests.Fakes.SimulationExtensions;

namespace DesktopPet.Core.Tests.Breeding;

/// <summary>What a child inherits, jacket colours, and eggs as world objects.</summary>
public class InheritanceTests
{
    private static readonly PetTraits Mia = new()
    {
        Name = "Mia",
        Neediness = new Neediness(2, 1, 0.5),
        Behavior = new BehaviorTraits(Walk: 2, Mating: 3),
        Chat = new ChatTraits(Jokes: 0.5),
        Fixed = false,
    };

    private static readonly PetTraits Leo = new()
    {
        Name = "Leo", Neediness = new Neediness(1, 1, 1.5), Jacket = "#B0384A", Character = "emberdonkey",
    };

    [Fact]
    public void Each_number_comes_from_one_parent_or_the_average()
    {
        // Rolls: which (a, b, average) then the variation (0.5 = none).
        Assert.Equal(2, Inheritance.Gene(2, 1, 0.15, new FakeRandom(0.1, 0.5)));
        Assert.Equal(1, Inheritance.Gene(2, 1, 0.15, new FakeRandom(0.5, 0.5)));
        Assert.Equal(1.5, Inheritance.Gene(2, 1, 0.15, new FakeRandom(0.9, 0.5)));
    }

    [Fact]
    public void Variation_stays_within_its_factor()
    {
        Assert.Equal(System.Math.Round(2 / 1.15, 2), Inheritance.Gene(2, 2, 0.15, new FakeRandom(0.0, 0.0)), 2);
        Assert.Equal(System.Math.Round(2 * 1.15, 2), Inheritance.Gene(2, 2, 0.15, new FakeRandom(0.0, 0.99999)), 2);
        Assert.Equal(10, Inheritance.Gene(10, 10, 0.5, new FakeRandom(0.0, 0.99999))); // capped at the trait maximum
    }

    [Fact]
    public void The_egg_holds_a_nameless_unfixed_child_with_its_parents_and_a_blended_jacket()
    {
        var now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var egg = Inheritance.Egg(new Parent(1, Mia, false), new Parent(4, Leo, true), new BreedingRules(), new FakeRandom(0.0), now);

        Assert.Equal(now, egg.LaidAtUtc);
        Assert.Equal(now + TimeSpan.FromMinutes(60), egg.HatchAtUtc);
        Assert.Equal(["Mia", "Leo"], egg.Parents);
        Assert.Equal("Mia and Leo", egg.ParentNames);
        Assert.Equal(string.Empty, egg.Child.Name);
        Assert.False(egg.Child.Fixed);
        Assert.Equal(egg.Parents, egg.Child.Parents);

        // FakeRandom(0.0): every number from the first parent, at the low end of the variation.
        Assert.Equal(System.Math.Round(2 / 1.15, 2), egg.Child.Neediness.Hunger);
        Assert.Equal(System.Math.Round(3 / 1.15, 2), egg.Child.Behavior.Mating);

        // Mia wears pet 1's charcoal (3E455A), Leo red (B0384A): half and half, less 14 a channel (48.5 rounds to even).
        Assert.True(JacketColors.TryParse(egg.Child.Jacket, out var jacket));
        Assert.Equal("#693044", JacketColors.Format(jacket));

        // The first parent's art (0.0 < 0.5), which has no sprites of its own: its character is pet 1's default.
        Assert.Null(egg.SpritesFrom);
        Assert.Equal(Characters.For(1, null).Id, egg.Child.Character);
        var other = Inheritance.Egg(new Parent(1, Mia, false), new Parent(4, Leo, true), new BreedingRules(), new FakeRandom(0.9), now);
        Assert.Equal(4, other.SpritesFrom);
        Assert.Equal("emberdonkey", other.Child.Character);
    }

    [Fact]
    public void Jacket_colours_follow_the_pet_number_unless_it_has_its_own()
    {
        Assert.Equal(JacketColors.Variants[0], JacketColors.For(1, null));
        Assert.Equal(JacketColors.Variants[1], JacketColors.For(2, null));
        Assert.Equal(JacketColors.Variants[0], JacketColors.For(6, null));
        Assert.Equal(JacketColors.Variants[3], JacketColors.For(1, "#b0384a")); // a variant's colour gets its exact palette

        var own = JacketColors.For(1, "#808080");
        Assert.Equal(0xFF808080, own.Base);
        Assert.True((own.Shade & 0xFF) < 0x80 && (own.Light & 0xFF) > 0x80);
    }

    [Fact]
    public void Jacket_colours_parse_and_format()
    {
        Assert.True(JacketColors.TryParse("3e455a", out var c));
        Assert.Equal(0xFF3E455A, c);
        Assert.Equal("#3E455A", JacketColors.Format(c));
        Assert.False(JacketColors.TryParse("#12345", out _));
        Assert.False(JacketColors.TryParse("red", out _));
        Assert.False(JacketColors.TryParse(null, out _));
    }

    [Fact]
    public void Eggs_are_saved_with_what_they_hold_and_come_back()
    {
        var desktop = new FakeDesktop().Monitor(0, 0, 1920, 1080, taskbar: 40).Snapshot();
        var world = new FurnitureWorld(PetConfig.Default, [DefaultFurniture.Ball]);
        var egg = Inheritance.Egg(new Parent(1, Mia, false), new Parent(2, Leo, false), new BreedingRules(), new FakeRandom(0.3), DateTimeOffset.UnixEpoch);
        world.AddEgg(new Vec2(500, 900), egg);
        world.Add("ball", new Vec2(800, 900));
        for (var i = 0; i < 120; i++)
        {
            world.Tick(Dt, desktop);
        }

        Assert.Equal(1040, Assert.Single(world.EggItems).Feet.Y, 0);
        Assert.DoesNotContain(world.Specs, s => s.Kind == Eggs.Kind); // not offered as furniture

        var json = System.Text.Json.JsonSerializer.Serialize(world.CreateSave());
        var loaded = new FurnitureWorld(PetConfig.Default, [DefaultFurniture.Ball]);
        loaded.Load(System.Text.Json.JsonSerializer.Deserialize<RoomSave>(json)!);
        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(egg, Assert.Single(loaded.EggItems).Egg);

        loaded.SetSpecs([]); // every furniture file deleted: the egg stays
        Assert.Single(loaded.Items);
        Assert.NotNull(loaded.Items[0].Egg);
    }

    [Fact]
    public void A_saved_egg_without_its_contents_is_dropped()
    {
        var world = new FurnitureWorld(PetConfig.Default);
        world.Load(new RoomSave { Items = [new RoomItem(Eggs.Kind, 100, 100)] });
        Assert.Empty(world.Items);
    }
}
