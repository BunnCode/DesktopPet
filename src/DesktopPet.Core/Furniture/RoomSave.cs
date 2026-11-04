using System.Text.Json;

namespace DesktopPet.Core.Furniture;

/// <summary>Where one piece of furniture was: its kind and feet point (physical pixels).</summary>
public sealed record RoomItem(string Kind, double X, double Y);

/// <summary>The furniture that survives a restart (<c>room.json</c>). Versioned for future migrations.</summary>
public sealed record RoomSave
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public IReadOnlyList<RoomItem> Items { get; init; } = [];
}

/// <summary>
/// <c>room.json</c> in the data folder, written atomically (temp file, then swap). A missing or corrupt file loads as
/// an empty room.
/// </summary>
public sealed class RoomStore(string path)
{
    public const string FileName = "room.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string Path { get; } = path;

    public RoomSave Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new RoomSave();
            }

            var save = JsonSerializer.Deserialize<RoomSave>(File.ReadAllText(Path), Options);
            return save is { Version: >= 1 } ? save : new RoomSave();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new RoomSave();
        }
    }

    public void Save(RoomSave save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(save, Options));
        File.Move(temp, Path, overwrite: true);
    }
}
