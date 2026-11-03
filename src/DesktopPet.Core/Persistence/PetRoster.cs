using System.Globalization;

namespace DesktopPet.Core.Persistence;

/// <summary>
/// Which pets exist is simply which save files exist: pet 1 is <c>save.json</c> (the original single-pet file, so old
/// saves carry over) and pet <i>n</i> is <c>save-n.json</c>.
/// </summary>
public static class PetRoster
{
    public const int MaxPets = 8;

    public static string SaveFileName(int petId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(petId, 1);
        return petId == 1 ? "save.json" : $"save-{petId.ToString(CultureInfo.InvariantCulture)}.json";
    }

    /// <summary>Pet ids for the save files present (any order in, ascending out); unrelated files are ignored.</summary>
    public static IReadOnlyList<int> IdsFromFileNames(IEnumerable<string> fileNames)
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

    /// <summary>The pets to start with: the saved ones (up to the limit), or pet 1 when there are none.</summary>
    public static IReadOnlyList<int> StartupPets(IEnumerable<string> fileNames)
    {
        var ids = IdsFromFileNames(fileNames);
        return ids.Count == 0 ? [1] : [.. ids.Take(MaxPets)];
    }

    /// <summary>The id for a new pet: one more than any in use (ids are never reused while running).</summary>
    public static int NextId(IEnumerable<int> inUse)
    {
        ArgumentNullException.ThrowIfNull(inUse);
        return inUse.DefaultIfEmpty(0).Max() + 1;
    }
}
