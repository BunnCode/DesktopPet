using System.Text.Json;
using Deskling.Core.Simulation;

namespace Deskling.Core.Persistence;

/// <summary>The outcome of reading settings: a usable config, or the reasons it can't be used.</summary>
/// <param name="Config">The settings, or null if there were errors.</param>
/// <param name="Errors">What's wrong, in terms of the file's keys.</param>
public sealed record ConfigParseResult(PetConfig? Config, IReadOnlyList<string> Errors);

/// <summary>
/// <c>settings.json</c>: the whole <see cref="PetConfig"/> as JSON. Forgiving to hand-edit (comments, trailing commas,
/// any key casing, missing keys keep their defaults, unknown keys ignored) and strict about values (range-checked).
/// </summary>
public static class PetConfigFile
{
    public const string Header =
        "// Deskling settings. Edit and save: changes apply right away (StepSeconds and MaxStepsPerFrame on restart).\n" +
        "// Distances and speeds are in DIPs (they scale with your display). Remove a line to get its default back,\n" +
        "// or delete this file to restore all defaults. Comments like these are allowed.\n";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string Serialize(PetConfig config) => Header + JsonSerializer.Serialize(config, Options) + "\n";

    public static ConfigParseResult Parse(string text)
    {
        PetConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PetConfig>(text, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" (line {line + 1})" : string.Empty;
            return new ConfigParseResult(null, [$"Not valid JSON{where}: {FirstSentence(e.Message)}"]);
        }

        if (config is null)
        {
            return new ConfigParseResult(null, ["The file is empty."]);
        }

        var errors = PetConfigValidator.Validate(config);
        return new ConfigParseResult(errors.Count == 0 ? config : null, errors);
    }

    private static string FirstSentence(string message)
    {
        var end = message.IndexOf(". ", StringComparison.Ordinal);
        return end > 0 ? message[..(end + 1)] : message;
    }
}
