using System.Globalization;
using DesktopPet.Core.Stats;

namespace DesktopPet.Core.Simulation;

/// <summary>
/// Range checks for a <see cref="PetConfig"/> loaded from a user-edited file. Messages name the JSON keys
/// (e.g. <c>Stats.Critical</c>) so they can be fixed in the file.
/// </summary>
public static class PetConfigValidator
{
    public static IReadOnlyList<string> Validate(PetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var c = config;
        var errors = new List<string>();

        void Range(string name, double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} must be between {min} and {max} (it is {value})."));
            }
        }

        void Duration(string name, DurationRange d)
        {
            if (!double.IsFinite(d.Min) || !double.IsFinite(d.Max) || d.Min < 0 || d.Max < d.Min || d.Max > 86_400)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} needs 0 <= Min <= Max <= 86400 seconds (it is {d.Min}..{d.Max})."));
            }
        }

        void TraitRange(string name, Traits.TraitRange r)
        {
            var max = Traits.PetTraitsFile.MaxMultiplier;
            if (!double.IsFinite(r.Min) || !double.IsFinite(r.Max) || r.Min < 0.1 || r.Max < r.Min || r.Max > max)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture, $"{name} needs 0.1 <= Min <= Max <= {max} (it is {r.Min}..{r.Max})."));
            }
        }

        void Chances(string names, params double[] values)
        {
            if (values.Sum() > 1 + 1e-9)
            {
                errors.Add($"{names} together must not exceed 1.");
            }
        }

        // Size and timing
        Range(nameof(c.SizeDip), c.SizeDip, 16, 512);
        Range(nameof(c.BodyHalfWidthFraction), c.BodyHalfWidthFraction, 0.05, 0.5);
        Range(nameof(c.StepSeconds), c.StepSeconds, 1.0 / 240, 1.0 / 15);
        Range(nameof(c.MaxStepsPerFrame), c.MaxStepsPerFrame, 1, 20);

        // Physics
        Range(nameof(c.GravityDip), c.GravityDip, 1, 100_000);
        Range(nameof(c.TerminalVelocityDip), c.TerminalVelocityDip, 1, 100_000);
        Range(nameof(c.AirDragPerSecond), c.AirDragPerSecond, 0, 100);
        Range(nameof(c.WallRestitution), c.WallRestitution, 0, 1);
        Range(nameof(c.GroundFrictionDip), c.GroundFrictionDip, 0, 100_000);
        Range(nameof(c.LostGraceSeconds), c.LostGraceSeconds, 0, 60);

        // Mouse
        Range(nameof(c.DragThresholdDip), c.DragThresholdDip, 0, 200);
        Range(nameof(c.ThrowWindowSeconds), c.ThrowWindowSeconds, 0.01, 1);
        Range(nameof(c.MaxThrowSpeedDip), c.MaxThrowSpeedDip, 1, 100_000);

        // Behavior
        Range(nameof(c.WalkSpeedDip), c.WalkSpeedDip, 0, 5_000);
        var w = c.Weights;
        if (w is null)
        {
            errors.Add("Weights is missing.");
        }
        else
        {
            Range("Weights.Idle", w.Idle, 0, 1_000);
            Range("Weights.Walk", w.Walk, 0, 1_000);
            Range("Weights.Sit", w.Sit, 0, 1_000);
            Range("Weights.Sleep", w.Sleep, 0, 1_000);
            if (w.Idle + w.Walk + w.Sit <= 0)
            {
                errors.Add("Weights.Idle, Weights.Walk and Weights.Sit can't all be 0.");
            }
        }

        Duration(nameof(c.IdleSeconds), c.IdleSeconds);
        Duration(nameof(c.WalkSeconds), c.WalkSeconds);
        Duration(nameof(c.SitSeconds), c.SitSeconds);
        Duration(nameof(c.NapSeconds), c.NapSeconds);
        Duration(nameof(c.HappySeconds), c.HappySeconds);
        Range(nameof(c.EatSeconds), c.EatSeconds, 0, 60);
        Range(nameof(c.PlayHops), c.PlayHops, 0, 20);
        Range(nameof(c.PlayHopSpeedDip), c.PlayHopSpeedDip, 0, 10_000);
        Range(nameof(c.WalkTurnChance), c.WalkTurnChance, 0, 1);

        // New pets
        TraitRange(nameof(c.NewPetNeediness), c.NewPetNeediness);
        TraitRange(nameof(c.NewPetBehavior), c.NewPetBehavior);
        TraitRange(nameof(c.NewPetChat), c.NewPetChat);

        // Walls
        Range(nameof(c.ClimbChance), c.ClimbChance, 0, 1);
        Range(nameof(c.PushChance), c.PushChance, 0, 1);
        Chances("ClimbChance and PushChance", c.ClimbChance, c.PushChance);
        Range(nameof(c.ClimbDownChance), c.ClimbDownChance, 0, 1);
        Range(nameof(c.ClimbSpeedDip), c.ClimbSpeedDip, 0, 5_000);
        Range(nameof(c.ClingMaxSpeedDip), c.ClingMaxSpeedDip, 0, 100_000);
        Range(nameof(c.ClingReachDip), c.ClingReachDip, 0, 500);
        Duration(nameof(c.ClimbSeconds), c.ClimbSeconds);
        Duration(nameof(c.ClingSeconds), c.ClingSeconds);
        Duration(nameof(c.PushSeconds), c.PushSeconds);
        Range(nameof(c.PushSpeedDip), c.PushSpeedDip, 0, 2_000);

        // Hanging
        Range(nameof(c.CaptionHeightDip), c.CaptionHeightDip, 0, 200);
        Range(nameof(c.HangReachDip), c.HangReachDip, 0, 500);
        Range(nameof(c.HangCatchMaxSpeedDip), c.HangCatchMaxSpeedDip, 0, 100_000);
        Duration(nameof(c.HangSeconds), c.HangSeconds);
        Duration(nameof(c.ShimmySeconds), c.ShimmySeconds);
        Range(nameof(c.ShimmySpeedDip), c.ShimmySpeedDip, 0, 5_000);
        Range(nameof(c.HangShimmyChance), c.HangShimmyChance, 0, 1);
        Range(nameof(c.HangDropChance), c.HangDropChance, 0, 1);
        Chances("HangShimmyChance and HangDropChance", c.HangShimmyChance, c.HangDropChance);

        var f = c.Furniture;
        if (f is null)
        {
            errors.Add("Furniture is missing.");
        }
        else
        {
            Range("Furniture.NeedBelow", f.NeedBelow, 0, 100);
            Range("Furniture.Weight", f.Weight, 0, 1_000);
            Range("Furniture.CuriosityWeight", f.CuriosityWeight, 0, 1_000);
            Range("Furniture.ErrandSeconds", f.ErrandSeconds, 1, 3_600);
            Range("Furniture.RetrySeconds", f.RetrySeconds, 0, 3_600);
            Range("Furniture.KickSpeedDip", f.KickSpeedDip, 0, 5_000);
            Range("Furniture.KickLiftDip", f.KickLiftDip, 0, 5_000);
            Range("Furniture.HopSpeedDip", f.HopSpeedDip, 0, 5_000);
            Range("Furniture.BounceHops", f.BounceHops, 0, 20);
            Range("Furniture.BounceHopSpeedDip", f.BounceHopSpeedDip, 0, 10_000);
            Range("Furniture.BounceLaunchDip", f.BounceLaunchDip, 0, 10_000);
        }

        var chat = c.Chat;
        if (chat is null)
        {
            errors.Add("Chat is missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(chat.ModelFile) || chat.ModelFile.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                errors.Add("Chat.ModelFile must be a file name or path.");
            }

            Range("Chat.MaxTokens", chat.MaxTokens, 8, 256);
            Range("Chat.Temperature", chat.Temperature, 0, 2);
            Range("Chat.NeedyBelow", chat.NeedyBelow, 0, 100);
            Range("Chat.NeedyPerHour", chat.NeedyPerHour, 0, 3_600);
            Range("Chat.BanterPerHour", chat.BanterPerHour, 0, 3_600);
            Range("Chat.BanterDistanceDip", chat.BanterDistanceDip, 0, 100_000);
            Range("Chat.JokesPerHour", chat.JokesPerHour, 0, 3_600);
            Range("Chat.NotesPerHour", chat.NotesPerHour, 0, 3_600);
            Range("Chat.MaxNotes", chat.MaxNotes, 0, 50);
            Range("Chat.NoteGapSeconds", chat.NoteGapSeconds, 0, 86_400);
            Range("Chat.PetNoteGapSeconds", chat.PetNoteGapSeconds, 0, 86_400);
            Range("Chat.ClickLineWithinSeconds", chat.ClickLineWithinSeconds, 0, 600);
            Range("Chat.MinGapSeconds", chat.MinGapSeconds, 0, 86_400);
            Range("Chat.PetGapSeconds", chat.PetGapSeconds, 0, 86_400);
            Range("Chat.ReplyTimeoutSeconds", chat.ReplyTimeoutSeconds, 1, 600);
            Range("Chat.AnswerDelaySeconds", chat.AnswerDelaySeconds, 0, 60);
            Range("Chat.BubbleSeconds", chat.BubbleSeconds, 0.5, 120);
            Range("Chat.BubbleSecondsPerChar", chat.BubbleSecondsPerChar, 0, 5);
            Range("Chat.RecentLines", chat.RecentLines, 0, 20);
            Range("Chat.RemarksPerHour", chat.RemarksPerHour, 0, 3_600);
            Range("Chat.NoteReadChance", chat.NoteReadChance, 0, 1);
            Range("Chat.ReadReachDip", chat.ReadReachDip, 0, 1_000);
            Range("Chat.ReadChars", chat.ReadChars, 40, 2_000);
            if (chat.ReadExcludedApps is null || chat.ReadExcludedApps.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add("Chat.ReadExcludedApps must be a list of app names (it can be empty: []).");
            }

            if (chat.ReadExcludedTitleWords is null || chat.ReadExcludedTitleWords.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add("Chat.ReadExcludedTitleWords must be a list of words (it can be empty: []).");
            }
        }

        ValidateStats(c.Stats, errors, Range);
        return errors;
    }

    private static void ValidateStats(StatRules? s, List<string> errors, Action<string, double, double, double> range)
    {
        if (s is null)
        {
            errors.Add("Stats is missing.");
            return;
        }

        range("Stats.HungerPerHour", s.HungerPerHour, -1_000, 1_000);
        range("Stats.HappinessPerHour", s.HappinessPerHour, -1_000, 1_000);
        range("Stats.EnergyPerHour", s.EnergyPerHour, -1_000, 1_000);
        range("Stats.ActiveEnergyPerHour", s.ActiveEnergyPerHour, -1_000, 1_000);
        range("Stats.SleepEnergyPerHour", s.SleepEnergyPerHour, -1_000, 1_000);
        range("Stats.SleepDecayFactor", s.SleepDecayFactor, 0, 10);
        range("Stats.Critical", s.Critical, 0, 100);
        range("Stats.AwayFloor", s.AwayFloor, 0, 100);
        if (s.AwayFloor < s.Critical)
        {
            errors.Add("Stats.AwayFloor must be at least Stats.Critical (nothing may reach critical while you're away).");
        }

        if (s.AwayGap < TimeSpan.FromSeconds(1) || s.AwayGap > TimeSpan.FromDays(1))
        {
            errors.Add($"Stats.AwayGap must be between 00:00:01 and 1.00:00:00 (it is {s.AwayGap}).");
        }

        range("Stats.FeedHunger", s.FeedHunger, -100, 100);
        range("Stats.FeedHappiness", s.FeedHappiness, -100, 100);
        range("Stats.PlayHappiness", s.PlayHappiness, -100, 100);
        range("Stats.PlayEnergy", s.PlayEnergy, -100, 100);
        range("Stats.PlayHunger", s.PlayHunger, -100, 100);
        range("Stats.PetHappiness", s.PetHappiness, -100, 100);
        range("Stats.WellRested", s.WellRested, 0, 100);
        range("Stats.Sad", s.Sad, 0, 100);
        range("Stats.Tired", s.Tired, 1, 100);
    }
}
