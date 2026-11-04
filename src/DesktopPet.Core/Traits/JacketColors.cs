using System.Globalization;
using DesktopPet.Core.Abstractions;

namespace DesktopPet.Core.Traits;

/// <summary>A jacket's three colours in the built-in rabbit art, as 0xAARRGGBB.</summary>
public readonly record struct JacketShades(uint Base, uint Shade, uint Light);

/// <summary>
/// The built-in rabbit's jacket colours. A pet without a <see cref="PetTraits.Jacket"/> wears one of the
/// <see cref="Variants"/> picked by its number; a hatched pet has its own colour, blended from its parents'.
/// </summary>
public static class JacketColors
{
    /// <summary>Charcoal (the colours drawn in the sheet), blue, green, red, purple.</summary>
    public static IReadOnlyList<JacketShades> Variants { get; } =
    [
        new(0xFF3E455A, 0xFF2C3142, 0xFF58617A),
        new(0xFF2F64B8, 0xFF214A8C, 0xFF5A8AD8),
        new(0xFF3E8A4A, 0xFF2C6636, 0xFF64AE6E),
        new(0xFFB0384A, 0xFF842836, 0xFFD06070),
        new(0xFF6E4AAE, 0xFF513586, 0xFF9474CE),
    ];

    /// <summary>How far each channel of a child's colour may stray from the blend of its parents', either way.</summary>
    public const int BlendVariation = 14;

    /// <summary>The jacket pet <paramref name="petId"/> wears: its own colour if it has one, else its number's variant.</summary>
    public static JacketShades For(int petId, string? jacket) =>
        TryParse(jacket, out var color) ? ShadesOf(color) : Variants[(((petId - 1) % Variants.Count) + Variants.Count) % Variants.Count];

    /// <summary>The base colour pet <paramref name="petId"/> wears, as #RRGGBB (for blending into a child's).</summary>
    public static string BaseOf(int petId, string? jacket) => Format(For(petId, jacket).Base);

    /// <summary>
    /// Shade and highlight for a base colour, the way the sheet's variants are drawn: the shade about three quarters as
    /// bright, the highlight a little lighter. A variant's base gets its exact palette.
    /// </summary>
    public static JacketShades ShadesOf(uint color)
    {
        color |= 0xFF000000;
        foreach (var v in Variants)
        {
            if (v.Base == color)
            {
                return v;
            }
        }

        static uint Map(uint c, Func<double, double> f)
        {
            uint Channel(int shift) => (uint)System.Math.Clamp((int)System.Math.Round(f((c >> shift) & 0xFF)), 0, 255) << shift;
            return 0xFF000000 | Channel(16) | Channel(8) | Channel(0);
        }

        return new JacketShades(color, Map(color, v => v * 0.73), Map(color, v => v + (0.08 * (255 - v)) + 26));
    }

    /// <summary>The parents' colours mixed half and half, each channel nudged by up to <see cref="BlendVariation"/>.</summary>
    public static string Blend(string a, string b, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var ca = TryParse(a, out var pa) ? pa : Variants[0].Base;
        var cb = TryParse(b, out var pb) ? pb : Variants[0].Base;
        uint Channel(int shift)
        {
            var mixed = ((((ca >> shift) & 0xFF) + ((cb >> shift) & 0xFF)) / 2.0) + random.Range(-BlendVariation, BlendVariation);

            // Keep it a jacket: not so dark the shading vanishes, not so light it reads as the shirt.
            return (uint)System.Math.Clamp((int)System.Math.Round(mixed), 24, 200) << shift;
        }

        return Format(0xFF000000 | Channel(16) | Channel(8) | Channel(0));
    }

    /// <summary>Reads #RRGGBB (the # optional, any case).</summary>
    public static bool TryParse(string? text, out uint color)
    {
        color = 0;
        var hex = text?.Trim().TrimStart('#');
        if (hex is not { Length: 6 } || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = 0xFF000000 | rgb;
        return true;
    }

    public static string Format(uint color) => string.Create(CultureInfo.InvariantCulture, $"#{color & 0xFFFFFF:X6}");
}
