using System.Diagnostics;
using System.IO;
using DesktopPet.App.Furniture;
using DesktopPet.Core.Furniture;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.Traits;
using Microsoft.VisualBasic.FileIO;

namespace DesktopPet.App.Customization;

/// <summary>
/// <c>%LOCALAPPDATA%\DesktopPet</c>: settings.json, the shared sprites folder, the exported sprite template,
/// <c>pets\n\</c>, one folder per pet with its save.json (stats and traits), personality.txt and own sprites, the furniture
/// definitions and art in <c>furniture\</c>, and where the furniture stands in room.json.
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
    public string Furniture => Path.Combine(Root, FurnitureSpecFile.FolderName);
    public string Room => Path.Combine(Root, RoomStore.FileName);
    public string PersonalityTraitsPath => Path.Combine(Root, PersonalityTraits.FileName);

    /// <summary>A second place for chat models (the first is the <c>models</c> folder next to the app).</summary>
    public string Models => Path.Combine(Root, "models");

    /// <summary>
    /// Finds the chat model: a full path as is; a file name in the <c>models</c> folder next to the app, then in the
    /// data folder's. Returns null when it's in none of them, with the places looked in.
    /// </summary>
    public (string? Path, IReadOnlyList<string> Searched) FindModel(string modelFile)
    {
        if (Path.IsPathRooted(modelFile))
        {
            return (File.Exists(modelFile) ? modelFile : null, [modelFile]);
        }

        string[] candidates = [Path.Combine(AppContext.BaseDirectory, "models", modelFile), Path.Combine(Models, modelFile)];
        return (candidates.FirstOrDefault(File.Exists), candidates);
    }

    public string PetFolder(int petId) => Path.Combine(Pets, PetRoster.FolderName(petId));
    public string SavePath(int petId) => Path.Combine(PetFolder(petId), PetRoster.SaveFileName);
    public string LegacyTraitsPath(int petId) => Path.Combine(PetFolder(petId), PetTraitsFile.LegacyFileName);
    public string PersonalityPath(int petId) => Path.Combine(PetFolder(petId), Personality.FileName);
    public string PetSprites(int petId) => Path.Combine(PetFolder(petId), PetRoster.SpritesFolderName);

    /// <summary>Sprite folders for a pet, most specific first: its own, then the shared one.</summary>
    public IReadOnlyList<string> SpriteFolders(int petId) => [PetSprites(petId), Sprites];

    /// <summary>Ids of the pet folders that hold a pet (a save, or an old traits.json not yet folded in), ascending.</summary>
    public IReadOnlyList<int> ExistingPets() =>
        [.. PetRoster.IdsFromFolderNames(Directory.EnumerateDirectories(Pets).Select(d => Path.GetFileName(d)))
            .Where(id => File.Exists(SavePath(id)) || File.Exists(LegacyTraitsPath(id)))];

    /// <summary>Pets to start with: one per pet folder except the dead ones, or pet 1 if there are no pet folders.</summary>
    public IReadOnlyList<int> StartupPets() => PetRoster.StartupPets(ExistingPets(), [.. DeadPets().Select(d => d.Id)]);

    /// <summary>The dead pets (their save is marked dead) with their saves, ascending.</summary>
    public IReadOnlyList<(int Id, SaveData Save)> DeadPets() =>
        [.. ExistingPets()
            .Select(id => (Id: id, Save: new JsonFileSaveStore(SavePath(id)).Load()))
            .Where(p => p.Save is { IsDead: true })
            .Select(p => (p.Id, p.Save!))];

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

    /// <summary>
    /// Folds each pet's old traits.json (from before version 4 of the save) into its save.json as the Traits section,
    /// then moves the traits.json to the Recycle Bin. Returns what went wrong; a file that couldn't be folded in stays.
    /// </summary>
    public IReadOnlyList<string> MigrateTraitsFiles()
    {
        var problems = new List<string>();
        foreach (var id in PetRoster.IdsFromFolderNames(Directory.EnumerateDirectories(Pets).Select(d => Path.GetFileName(d))))
        {
            var traitsPath = LegacyTraitsPath(id);
            if (!File.Exists(traitsPath))
            {
                continue;
            }

            try
            {
                var savePath = SavePath(id);
                var saveText = File.Exists(savePath) ? File.ReadAllText(savePath) : null;
                var (text, error) = PetSaveFile.FoldIn(saveText, File.ReadAllText(traitsPath), DateTimeOffset.UtcNow);
                if (text is null)
                {
                    problems.Add($@"pets\{id}\{error} So traits.json wasn't moved into save.json yet: fix it and restart.");
                    continue;
                }

                var temp = savePath + ".tmp";
                File.WriteAllText(temp, text);
                File.Move(temp, savePath, overwrite: true);
                FileSystem.DeleteFile(traitsPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                problems.Add($@"pets\{id}\traits.json couldn't be moved into its save.json: {e.Message}");
            }
        }

        return problems;
    }

    /// <summary>The Traits in a pet's save.json, or null if it has no save or no Traits yet.</summary>
    public TraitsParseResult? LoadTraits(int petId)
    {
        var path = SavePath(petId);
        try
        {
            return File.Exists(path) ? PetSaveFile.Parse(File.ReadAllText(path)).Traits : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new TraitsParseResult(null, [$"save.json couldn't be read: {e.Message}"]);
        }
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

    /// <summary>Whether the pet has its own sprite sheet (an atlas.json in its sprites folder).</summary>
    public bool HasOwnSprites(int petId) => File.Exists(Path.Combine(PetSprites(petId), SpriteSheet.SpriteAtlas.AtlasFileName));

    /// <summary>Copies one pet's own sprites folder into another's (a hatchling taking after a parent).</summary>
    public void CopySprites(int fromPetId, int toPetId)
    {
        var from = PetSprites(fromPetId);
        var to = PetSprites(toPetId);
        foreach (var file in Directory.EnumerateFiles(from, "*", System.IO.SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// The personality traits new pets are given, from personality-traits.json (written with the defaults when
    /// missing). A broken or empty file gives the defaults, with the problems.
    /// </summary>
    public (IReadOnlyList<PersonalityTrait> Traits, IReadOnlyList<string> Errors) LoadPersonalityTraits()
    {
        try
        {
            if (!File.Exists(PersonalityTraitsPath))
            {
                File.WriteAllText(PersonalityTraitsPath, PersonalityTraits.Serialize(PersonalityTraits.Defaults));
                return (PersonalityTraits.Defaults, []);
            }

            var parsed = PersonalityTraits.Parse(File.ReadAllText(PersonalityTraitsPath));
            return parsed.Traits is { } traits ? (traits, []) : (PersonalityTraits.Defaults, parsed.Errors);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (PersonalityTraits.Defaults, [$"couldn't be read: {e.Message}"]);
        }
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

    /// <summary>The list of default furniture kinds already written into the furniture folder.</summary>
    public string WrittenDefaults => Path.Combine(Furniture, "defaults-written.txt");

    /// <summary>
    /// Writes the default furniture, a definition and an image per kind: all of it on first run (no furniture folder
    /// yet), and later only defaults added since (such as the breeding pill), going by <see cref="WrittenDefaults"/>.
    /// Otherwise the folder is the user's: deleted files stay deleted. Returns what went wrong, if anything.
    /// </summary>
    public IReadOnlyList<string> WriteDefaultFurniture()
    {
        try
        {
            IReadOnlyCollection<string>? written = null;
            if (!Directory.Exists(Furniture))
            {
                Directory.CreateDirectory(Furniture);
                written = [];
            }
            else if (File.Exists(WrittenDefaults))
            {
                written = [.. File.ReadAllLines(WrittenDefaults).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))];
            }

            var toWrite = DefaultFurniture.ToWrite(written);
            foreach (var spec in toWrite)
            {
                var path = Path.Combine(Furniture, spec.Kind + FurnitureSpecFile.Extension);
                if (File.Exists(path))
                {
                    continue; // the user made one of that name: theirs stays
                }

                File.WriteAllText(path, FurnitureSpecFile.Serialize(spec));
                if (FurnitureArt.DefaultPng(spec.Kind) is { } png && !File.Exists(Path.Combine(Furniture, spec.Art)))
                {
                    File.WriteAllBytes(Path.Combine(Furniture, spec.Art), png);
                }
            }

            if (toWrite.Count > 0 || !File.Exists(WrittenDefaults))
            {
                File.WriteAllLines(WrittenDefaults,
                [
                    "# Default furniture already put in this folder (new defaults in later versions are added once).",
                    "# Remove a line to get that piece back the next time the app starts.",
                    .. DefaultFurniture.All.Select(s => s.Kind),
                ]);
            }

            return [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [$"The default furniture couldn't be written: {e.Message}"];
        }
    }

    /// <summary>
    /// Every furniture definition in the furniture folder, the defaults' kinds first (in their order), then the rest
    /// by name. A file that can't be read right now (being saved) comes back as an error.
    /// </summary>
    public IReadOnlyList<(string Kind, FurnitureParseResult Result)> LoadFurniture(IReadOnlyCollection<string> animations)
    {
        if (!Directory.Exists(Furniture))
        {
            return [];
        }

        var order = DefaultFurniture.All.Select(s => s.Kind).ToList();
        var results = new List<(string Kind, FurnitureParseResult Result)>();
        foreach (var path in Directory.EnumerateFiles(Furniture, "*" + FurnitureSpecFile.Extension))
        {
            if (FurnitureSpecFile.KindFromFileName(Path.GetFileName(path)) is not { } kind)
            {
                continue;
            }

            try
            {
                results.Add((kind, FurnitureSpecFile.Parse(kind, File.ReadAllText(path), animations)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                results.Add((kind, new FurnitureParseResult(null, [$"couldn't be read: {e.Message}"])));
            }
        }

        int Rank(string kind) => order.IndexOf(kind) is var i && i >= 0 ? i : int.MaxValue;
        return [.. results.OrderBy(r => Rank(r.Kind)).ThenBy(r => r.Kind, StringComparer.OrdinalIgnoreCase)];
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
