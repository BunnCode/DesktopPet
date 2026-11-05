using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deskling.Core.Furniture;

/// <summary>The outcome of reading a furniture definition: a usable spec, or the reasons it can't be used.</summary>
public sealed record FurnitureParseResult(FurnitureSpec? Spec, IReadOnlyList<string> Errors);

/// <summary>
/// One furniture definition (<c>furniture\&lt;kind&gt;.json</c>). Forgiving to hand-edit like <c>settings.json</c>
/// (comments, trailing commas, any key casing, missing keys keep their defaults) and strict about values.
/// </summary>
public static class FurnitureSpecFile
{
    public const string FolderName = "furniture";
    public const string Extension = ".json";

    public const int MaxSlots = 8;
    public const int MaxFrames = 64;
    public const double MaxSizeDip = 512;
    public const int MaxNameLength = 24;

    public const string Header =
        "// A kind of furniture. Edit and save: changes apply right away. Sizes are DIPs (the pet is 64).\n" +
        "// Art: an image next to this file, its Frames side by side. Animate: Always, Moving or InUse.\n" +
        "// Uses: Need (Hunger, Happiness, Energy or None), RatePerHour (points added per hour of use),\n" +
        "// Animation (the pet's), Seconds, Effect (None, Hop, Bounce, Kick, Confine or Breed), SeatDip (sit this high),\n" +
        "// MinSeconds (stay at least this long, even once the need is full).\n" +
        "// Optional per slot: SlotOffsetsDip (where it stands), SlotSeatsDip (its seat height, instead of SeatDip),\n" +
        "// SlotFacing (Toward, Left or Right).\n" +
        "// Consumable: true if the piece is used up once a pet has finished using it (a pill).\n" +
        "// Delete this file to remove the furniture; delete the whole folder to get the defaults back.\n";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The kind a definition file name stands for (<c>Ball.json</c> → <c>ball</c>), or null if it isn't one.</summary>
    public static string? KindFromFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) || fileName.Length <= Extension.Length)
        {
            return null;
        }

        return fileName[..^Extension.Length].ToLowerInvariant();
    }

    public static string Serialize(FurnitureSpec spec) => Header + JsonSerializer.Serialize(spec, Options) + "\n";

    /// <param name="kind">The kind (from the file name).</param>
    /// <param name="text">The file's contents.</param>
    /// <param name="animations">Ids of the pet's animations, which uses may play.</param>
    public static FurnitureParseResult Parse(string kind, string text, IReadOnlyCollection<string> animations)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(animations);
        FurnitureSpec? spec;
        try
        {
            spec = JsonSerializer.Deserialize<FurnitureSpec>(text, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            return new FurnitureParseResult(null, [$"Not valid JSON{where}."]);
        }

        if (spec is null)
        {
            return new FurnitureParseResult(null, ["The file is empty."]);
        }

        spec = spec with
        {
            Kind = kind.ToLowerInvariant(),
            Name = spec.Name?.Trim() ?? string.Empty,
            Art = spec.Art?.Trim() ?? string.Empty,
            Uses = spec.Uses ?? [],
        };
        var errors = Validate(spec, animations);
        return new FurnitureParseResult(errors.Count == 0 ? spec : null, errors);
    }

    public static IReadOnlyList<string> Validate(FurnitureSpec spec, IReadOnlyCollection<string> animations)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(animations);
        var errors = new List<string>();

        void Range(string name, double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} must be between {min} and {max} (it is {value})."));
            }
        }

        if (spec.Name.Length == 0)
        {
            errors.Add("Name is missing.");
        }
        else if (spec.Name.Length > MaxNameLength || spec.Name.Any(char.IsControl))
        {
            errors.Add($"Name must be one line of at most {MaxNameLength} characters.");
        }

        Range(nameof(spec.WidthDip), spec.WidthDip, 8, MaxSizeDip);
        Range(nameof(spec.HeightDip), spec.HeightDip, 8, MaxSizeDip);
        if (spec.Art.Length == 0)
        {
            errors.Add("Art is missing (the name of an image next to this file).");
        }
        else if (spec.Art.IndexOfAny(['\\', '/', ':']) >= 0 || spec.Art.Contains("..", StringComparison.Ordinal))
        {
            errors.Add("Art must be a file name in the furniture folder, not a path.");
        }

        Range(nameof(spec.Frames), spec.Frames, 1, MaxFrames);
        Range(nameof(spec.Fps), spec.Fps, 0.1, 60);
        Range(nameof(spec.Slots), spec.Slots, 0, MaxSlots);
        Range(nameof(spec.Bounciness), spec.Bounciness, 0, 0.95);
        Range(nameof(spec.Friction), spec.Friction, 0, 10);
        if (spec.SlotOffsetsDip is { } offsets)
        {
            if (offsets.Count != spec.Slots)
            {
                errors.Add($"SlotOffsetsDip needs one offset per slot ({spec.Slots}), or leave it out.");
            }

            for (var i = 0; i < offsets.Count; i++)
            {
                Range($"SlotOffsetsDip[{i}]", offsets[i], -MaxSizeDip, MaxSizeDip);
            }
        }

        if (spec.SlotSeatsDip is { } seats)
        {
            if (seats.Count != spec.Slots)
            {
                errors.Add($"SlotSeatsDip needs one seat height per slot ({spec.Slots}), or leave it out.");
            }

            for (var i = 0; i < seats.Count; i++)
            {
                Range($"SlotSeatsDip[{i}]", seats[i], 0, MaxSizeDip);
            }
        }

        if (spec.SlotFacing is { } facing)
        {
            if (facing.Count != spec.Slots)
            {
                errors.Add($"SlotFacing needs one direction per slot ({spec.Slots}), or leave it out.");
            }

            if (facing.Any(f => !Enum.IsDefined(f)))
            {
                errors.Add("SlotFacing directions must be Toward, Left or Right.");
            }
        }

        for (var i = 0; i < spec.Uses.Count; i++)
        {
            var use = spec.Uses[i];
            var name = $"Uses[{i}]";
            if (use is null)
            {
                errors.Add($"{name} is empty.");
                continue;
            }

            Range($"{name}.RatePerHour", use.RatePerHour, 0, 100_000);
            Range($"{name}.SeatDip", use.SeatDip, 0, MaxSizeDip);
            Range($"{name}.MinSeconds", use.MinSeconds, 0, 86_400);
            var d = use.Seconds;
            if (!double.IsFinite(d.Min) || !double.IsFinite(d.Max) || d.Min < 0 || d.Max < d.Min || d.Max > 86_400)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name}.Seconds needs 0 <= Min <= Max <= 86400 (it is {d.Min}..{d.Max})."));
            }

            if (!animations.Contains(use.Animation ?? string.Empty))
            {
                errors.Add($"{name}.Animation '{use.Animation}' isn't one of the pet's animations ({string.Join(", ", animations)}).");
            }

            if (use.Effect != UseEffect.Confine && spec.Slots == 0)
            {
                errors.Add($"{name}: furniture with 0 Slots can't be used; give it Slots or remove the use.");
            }
        }

        return errors;
    }
}
