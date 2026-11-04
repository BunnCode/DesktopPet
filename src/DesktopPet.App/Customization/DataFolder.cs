using System.Diagnostics;
using System.IO;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Traits;
using Microsoft.VisualBasic.FileIO;

namespace DesktopPet.App.Customization;

/// <summary>
/// <c>%LOCALAPPDATA%\DesktopPet</c>: settings.json, the shared sprites folder, the exported sprite template, and
/// <c>pets\n\</c>, one folder per pet with its save, traits.json, personality.txt and own sprites.
/// </summary>
internal sealed class DataFolder(string root)
{
    public static DataFolder Default { get; } =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet"));

    public string Root { get; } = root;

    public string Settings => Path.Combine(Root, "settings.json");
    public string Sprites => Path.Combine(Root, "sprites");
    public string SpriteTemplate => Path.Combine(Root, "sprite-template");
    public string Problems => Path.Combine(Root, "problems.txt");
    public string Pets => Path.Combine(Root, PetRoster.PetsFolderName);

    public string PetFolder(int petId) => Path.Combine(Pets, PetRoster.FolderName(petId));
    public string SavePath(int petId) => Path.Combine(PetFolder(petId), PetRoster.SaveFileName);
    public string TraitsPath(int petId) => Path.Combine(PetFolder(petId), PetTraitsFile.FileName);
    public string PersonalityPath(int petId) => Path.Combine(PetFolder(petId), Personality.FileName);
    public string PetSprites(int petId) => Path.Combine(PetFolder(petId), PetRoster.SpritesFolderName);

    /// <summary>Sprite folders for a pet, most specific first: its own, then the shared one.</summary>
    public IReadOnlyList<string> SpriteFolders(int petId) => [PetSprites(petId), Sprites];

    /// <summary>Ids of the pet folders that hold a pet (a save or traits), ascending.</summary>
    public IReadOnlyList<int> ExistingPets() =>
        [.. PetRoster.IdsFromFolderNames(Directory.EnumerateDirectories(Pets).Select(d => Path.GetFileName(d)))
            .Where(id => File.Exists(SavePath(id)) || File.Exists(TraitsPath(id)))];

    /// <summary>Pets to start with: one per pet folder, or pet 1 if there are none.</summary>
    public IReadOnlyList<int> StartupPets() => PetRoster.StartupPets(ExistingPets());

    /// <summary>Creates the folders the user is expected to put things in.</summary>
    public void EnsureExists()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Sprites);
        Directory.CreateDirectory(Pets);
    }

    /// <summary>Creates a pet's folder and its (empty) sprites folder.</summary>
    public void EnsurePetFolder(int petId) => Directory.CreateDirectory(PetSprites(petId));

    /// <summary>
    /// Moves pre-M10 save files (<c>save.json</c>, <c>save-n.json</c> in the data folder) into pet folders.
    /// Returns what went wrong, if anything; a file that couldn't be moved stays where it was.
    /// </summary>
    public IReadOnlyList<string> MigrateLegacySaves()
    {
        var problems = new List<string>();
        var rootFiles = Directory.EnumerateFiles(Root).Select(f => Path.GetFileName(f));
        var folders = PetRoster.IdsFromFolderNames(Directory.EnumerateDirectories(Pets).Select(d => Path.GetFileName(d)));
        foreach (var (fileName, petId) in PetRoster.PlanMigration(rootFiles, folders))
        {
            try
            {
                EnsurePetFolder(petId);
                File.Move(Path.Combine(Root, fileName), SavePath(petId));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{fileName} couldn't be moved into pets\\{petId}: {e.Message}");
            }
        }

        return problems;
    }

    /// <summary>A pet's traits.json, or null if it has none yet.</summary>
    public TraitsParseResult? LoadTraits(int petId)
    {
        var path = TraitsPath(petId);
        try
        {
            return File.Exists(path) ? PetTraitsFile.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new TraitsParseResult(null, [$"traits.json couldn't be read: {e.Message}"]);
        }
    }

    public void WriteTraits(int petId, PetTraits traits)
    {
        EnsurePetFolder(petId);
        File.WriteAllText(TraitsPath(petId), PetTraitsFile.Serialize(traits));
    }

    /// <summary>A pet's personality text, or null if it has no personality.txt (or it can't be read right now).</summary>
    public string? LoadPersonality(int petId)
    {
        try
        {
            var path = PersonalityPath(petId);
            return File.Exists(path) ? Personality.Clean(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void WritePersonality(int petId, string text)
    {
        EnsurePetFolder(petId);
        File.WriteAllText(PersonalityPath(petId), text + "\n");
    }

    /// <summary>Moves a pet's whole folder to the Recycle Bin, so a removed pet (and its art) can still be restored.</summary>
    public void RecyclePetFolder(int petId)
    {
        var folder = PetFolder(petId);
        if (Directory.Exists(folder))
        {
            FileSystem.DeleteDirectory(folder, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
    }

    /// <summary>
    /// Reads settings.json, writing the defaults first if it doesn't exist yet. A file that can't be read at all
    /// (locked mid-save) counts as an error.
    /// </summary>
    public ConfigParseResult LoadSettings()
    {
        try
        {
            if (!File.Exists(Settings))
            {
                File.WriteAllText(Settings, PetConfigFile.Serialize(PetConfig.Default));
                return new ConfigParseResult(PetConfig.Default, []);
            }

            return PetConfigFile.Parse(File.ReadAllText(Settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ConfigParseResult(null, [$"settings.json couldn't be read: {e.Message}"]);
        }
    }

    /// <summary>Writes the full list of problems (notifications only have room for a summary).</summary>
    public void WriteProblems(string heading, IReadOnlyList<string> problems)
    {
        try
        {
            File.WriteAllText(Problems, $"{heading} ({DateTime.Now:g})\n\n- {string.Join("\n- ", problems)}\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"DesktopPet: couldn't write problems.txt: {e.Message}");
        }
    }

    /// <summary>Opens a file or folder the way Explorer would (default app for .json, Explorer for folders).</summary>
    public static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"DesktopPet: couldn't open {path}: {e.Message}");
        }
    }
}
