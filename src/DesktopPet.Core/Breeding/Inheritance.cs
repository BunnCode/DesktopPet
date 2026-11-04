using DesktopPet.Core.Abstractions;
using DesktopPet.Core.Chat;
using DesktopPet.Core.Traits;

namespace DesktopPet.Core.Breeding;

/// <summary>A parent as inheritance sees it.</summary>
/// <param name="Id">Its pet id (folder number): picks its jacket colour if it has none of its own.</param>
/// <param name="Traits">Its traits.</param>
/// <param name="HasOwnSprites">Whether it has its own sprite sheet (<c>pets\n\sprites\atlas.json</c>).</param>
public sealed record Parent(int Id, PetTraits Traits, bool HasOwnSprites);

/// <summary>
/// How a child takes after its parents: each number comes from one parent or their average, then strays a little
/// (<see cref="BreedingRules.Variation"/>). The built-in rabbit's jacket colour is a blend of the parents'. The art
/// comes from one parent at random: if that one has its own sprite sheet, the child gets a copy.
/// </summary>
public static class Inheritance
{
    /// <summary>What an egg laid now by <paramref name="a"/> and <paramref name="b"/> holds.</summary>
    public static EggInfo Egg(Parent a, Parent b, BreedingRules rules, IRandom random, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);
        var parents = new TextList([a.Traits.Name, b.Traits.Name]);
        var child = Cross(a.Traits, b.Traits, rules.Variation, random) with
        {
            Jacket = JacketColors.Blend(JacketColors.BaseOf(a.Id, a.Traits.Jacket), JacketColors.BaseOf(b.Id, b.Traits.Jacket), random),
            Parents = parents,
        };
        var art = random.Chance(0.5) ? a : b;
        return new EggInfo
        {
            LaidAtUtc = now,
            HatchAtUtc = now + TimeSpan.FromMinutes(rules.HatchMinutes),
            Parents = parents,
            Child = child,
            SpritesFrom = art.HasOwnSprites ? art.Id : null,
        };
    }

    /// <summary>The child's numbers (no name yet, not fixed).</summary>
    public static PetTraits Cross(PetTraits a, PetTraits b, double variation, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(random);
        double G(double x, double y) => Gene(x, y, variation, random);
        var (na, nb) = (a.Neediness, b.Neediness);
        var (ba, bb) = (a.Behavior, b.Behavior);
        var (ca, cb) = (a.Chat, b.Chat);
        return new PetTraits
        {
            Neediness = new Neediness(G(na.Hunger, nb.Hunger), G(na.Happiness, nb.Happiness), G(na.Energy, nb.Energy)),
            Behavior = new BehaviorTraits(
                G(ba.Idle, bb.Idle), G(ba.Walk, bb.Walk), G(ba.Sit, bb.Sit), G(ba.Sleep, bb.Sleep), G(ba.Climb, bb.Climb),
                G(ba.Push, bb.Push), G(ba.Mating, bb.Mating)),
            Chat = new ChatTraits(
                G(ca.Needy, cb.Needy), G(ca.Banter, cb.Banter), G(ca.Jokes, cb.Jokes), G(ca.Notes, cb.Notes), G(ca.Curiosity, cb.Curiosity)),
        };
    }

    /// <summary>One number: a's, b's or their average (a third each), times a factor within ÷(1+v)…×(1+v).</summary>
    internal static double Gene(double a, double b, double variation, IRandom random)
    {
        var pick = random.NextDouble();
        var value = pick < 1.0 / 3 ? a : pick < 2.0 / 3 ? b : (a + b) / 2;
        var spread = System.Math.Log(1 + System.Math.Max(0, variation));
        var factor = spread > 0 ? System.Math.Exp(random.Range(-spread, spread)) : 1;
        return System.Math.Round(System.Math.Clamp(value * factor, 0, PetTraitsFile.MaxMultiplier), 2);
    }
}
