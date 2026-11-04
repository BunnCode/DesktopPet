using System.Globalization;
using DesktopPet.Core.Traits;

namespace DesktopPet.Core.Persistence;

/// <summary>Which of a pet's files changed.</summary>
public enum PetFileKind
{
    /// <summary>Its save.json, which holds its traits (the app's own autosaves included).</summary>
    Save,
    Personality,
    Sprites,
}

/// <summary>
/// Which pets exist is which pet folders exist: pet <i>n</i> lives in <c>pets\n\</c> (its save with its traits, its personality
/// and sprites), so copying the folder moves the pet. Before M10 pets were bare save files in the data folder
/// (<c>save.json</c> for pet 1, <c>save-n.json</c> for pet <i>n</i>); <see cref="PlanMigration"/> moves those into folders.
/// </summary>
public static class PetRoster
{
    public const int MaxPets = 8;

    /// <summary>The folder (under the data folder) holding one subfolder per pet.</summary>
    public const string PetsFolderName = "pets";

    /// <summary>A pet's save, inside its folder.</summary>
    public const string SaveFileName = "save.json";

    /// <summary>A pet's own sprites, inside its folder.</summary>
    public const string SpritesFolderName = "sprites";

    public static string FolderName(int petId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(petId, 1);
        return petId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Pet ids for the folders present (any order in, ascending out); other names are ignored.</summary>
    public static IReadOnlyList<int> IdsFromFolderNames(IEnumerable<string> folderNames)
    {
        ArgumentNullException.ThrowIfNull(folderNames);
        var ids = new SortedSet<int>();
        foreach (var name in folderNames)
        {
            if (TryParseId(name, out var id))
            {
                ids.Add(id);
            }
        }

        return [.. ids];
    }

    /// <summary>
    /// The pets to start with: the existing ones that aren't dead (up to the limit), or pet 1 when there are no pet
    /// folders at all. When every pet is dead there are none: they have gravestones instead.
    /// </summary>
    public static IReadOnlyList<int> StartupPets(IEnumerable<int> existing, IEnumerable<int>? dead = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        var ids = existing.Distinct().Order().ToList();
        if (ids.Count == 0)
        {
            return [1];
        }

        var gone = new HashSet<int>(dead ?? []);
        return [.. ids.Where(id => !gone.Contains(id)).Take(MaxPets)];
    }

    /// <summary>The id for a new pet: one more than any in use (ids are never reused while running).</summary>
    public static int NextId(IEnumerable<int> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        return inUse.DefaultIfEmpty(0).Max() + 1;
    }

    /// <summary>Pre-M10 save file name of pet <paramref name="petId"/>: <c>save.json</c> or <c>save-n.json</c>.</summary>
    public static string LegacySaveFileName(int petId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(petId, 1);
        return petId == 1 ? "save.json" : $"save-{petId.ToString(CultureInfo.InvariantCulture)}.json";
    }

    /// <summary>Pet ids of pre-M10 save files among <paramref name="fileNames"/>, ascending.</summary>
    public static IReadOnlyList<int> LegacyIdsFromFileNames(IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        var ids = new SortedSet<int>();
        foreach (var name in fileNames)
        {
            if (string.Equals(name, "save.json", StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(1);
            }
            else if (name.StartsWith("save-", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(name.AsSpan(5, name.Length - 10), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && id >= 2)
            {
                ids.Add(id);
            }
        }

        return [.. ids];
    }

    /// <summary>
    /// Where each old save file in the data folder moves: into the folder with the same pet number, or, if that
    /// folder already exists, into a new one after every id in use. Nothing is ever overwritten.
    /// </summary>
    /// <param name="rootFileNames">File names in the data folder.</param>
    /// <param name="existingFolderIds">Ids of pet folders that already exist.</param>
    public static IReadOnlyList<(string FileName, int PetId)> PlanMigration(IEnumerable<string> rootFileNames, IEnumerable<int> existingFolderIds)
    {
        ArgumentNullException.ThrowIfNull(rootFileNames);
        ArgumentNullException.ThrowIfNull(existingFolderIds);
        var taken = new HashSet<int>(existingFolderIds);
        var legacy = LegacyIdsFromFileNames(rootFileNames);
        var moves = new List<(string, int)>();
        foreach (var id in legacy)
        {
            var target = taken.Contains(id) ? NextId(taken.Concat(legacy)) : id;
            taken.Add(target);
            moves.Add((LegacySaveFileName(id), target));
        }

        return moves;
    }

    /// <summary>
    /// What a change at <paramref name="relativePath"/> (relative to the pets folder, either slash) means: a pet's
    /// save (which holds its traits), personality or sprites. Null for anything else.
    /// </summary>
    public static (int PetId, PetFileKind Kind)? Classify(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !TryParseId(parts[0], out var id))
        {
            return null;
        }

        if (string.Equals(parts[1], SpritesFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return (id, PetFileKind.Sprites);
        }

        if (parts.Length != 2)
        {
            return null;
        }

        if (string.Equals(parts[1], SaveFileName, StringComparison.OrdinalIgnoreCase))
        {
            return (id, PetFileKind.Save);
        }

        return string.Equals(parts[1], Personality.FileName, StringComparison.OrdinalIgnoreCase)
            ? (id, PetFileKind.Personality)
            : null;
    }

    private static bool TryParseId(string name, out int id) =>
        int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id >= 1
        && string.Equals(name, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal); // no "01"
}
