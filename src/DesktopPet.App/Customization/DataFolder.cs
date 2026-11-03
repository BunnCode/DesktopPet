using System.Diagnostics;
using System.IO;
using DesktopPet.Core.Persistence;
using DesktopPet.Core.Simulation;

namespace DesktopPet.App.Customization;

/// <summary>
/// <c>%LOCALAPPDATA%\DesktopPet</c>: the save, settings.json, the sprites folder and the exported sprite template.
/// </summary>
internal sealed class DataFolder(string root)
{
    public static DataFolder Default { get; } =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPet"));

    public string Root { get; } = root;

    /// <summary>Save file of pet <paramref name="petId"/> (pet 1: save.json; pet n: save-n.json).</summary>
    public string SavePath(int petId) => Path.Combine(Root, PetRoster.SaveFileName(petId));

    /// <summary>Pets to start with: one per save file present, or pet 1 if there are none.</summary>
    public IReadOnlyList<int> StartupPets() =>
        PetRoster.StartupPets(Directory.EnumerateFiles(Root).Select(f => Path.GetFileName(f)));
    public string Settings => Path.Combine(Root, "settings.json");
    public string Sprites => Path.Combine(Root, "sprites");
    public string SpriteTemplate => Path.Combine(Root, "sprite-template");
    public string Problems => Path.Combine(Root, "problems.txt");

    /// <summary>Creates the folders the user is expected to put things in.</summary>
    public void EnsureExists()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Sprites);
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
