using System.IO;
using System.Windows.Threading;
using DesktopPet.Core.Persistence;

namespace DesktopPet.App.Customization;

/// <summary>
/// Watches the data folder for edits to settings.json, anything under sprites\ and furniture\, and each pet's traits,
/// personality and sprites, and reports them on the UI thread, debounced (editors often write a file several times in a
/// row, and art tools save in steps). Our own writes of saves, room.json, problems.txt and the template are ignored.
/// </summary>
internal sealed class CustomizationWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private readonly DataFolder _folder;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _settingsTimer;
    private readonly DispatcherTimer _spritesTimer;
    private readonly DispatcherTimer _furnitureTimer;
    private readonly Dictionary<(int, PetFileKind), DispatcherTimer> _petTimers = [];
    private readonly Action<int, PetFileKind> _onPetChanged;

    /// <param name="folder">The data folder.</param>
    /// <param name="onSettingsChanged">settings.json changed.</param>
    /// <param name="onSpritesChanged">The shared sprites folder changed.</param>
    /// <param name="onPetChanged">One of a pet's files changed (pet id, which kind).</param>
    /// <param name="onFurnitureChanged">Something in the furniture folder changed (a definition or its art).</param>
    public CustomizationWatcher(
        DataFolder folder, Action onSettingsChanged, Action onSpritesChanged, Action<int, PetFileKind> onPetChanged, Action onFurnitureChanged)
    {
        _folder = folder;
        _onPetChanged = onPetChanged;
        var dispatcher = Dispatcher.CurrentDispatcher;
        _settingsTimer = OneShot(dispatcher, onSettingsChanged);
        _spritesTimer = OneShot(dispatcher, onSpritesChanged);
        _furnitureTimer = OneShot(dispatcher, onFurnitureChanged);

        _watcher = new FileSystemWatcher(folder.Root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += (s, e) =>
        {
            OnFileEvent(s, e);
            Classify(e.OldFullPath, dispatcher);
        };
        _watcher.EnableRaisingEvents = true;

        void OnFileEvent(object? sender, FileSystemEventArgs e) => Classify(e.FullPath, dispatcher);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _settingsTimer.Stop();
        _spritesTimer.Stop();
        _furnitureTimer.Stop();
        foreach (var timer in _petTimers.Values)
        {
            timer.Stop();
        }
    }

    private static DispatcherTimer OneShot(Dispatcher dispatcher, Action action)
    {
        var timer = new DispatcherTimer(Debounce, DispatcherPriority.Background, (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            action();
        }, dispatcher);
        timer.Stop();
        return timer;
    }

    /// <summary>Raised on a thread-pool thread; restart the matching debounce timer on the UI thread.</summary>
    private void Classify(string path, Dispatcher dispatcher)
    {
        if (string.Equals(path, _folder.Settings, StringComparison.OrdinalIgnoreCase))
        {
            Restart(dispatcher, () => _settingsTimer);
        }
        else if (IsIn(path, _folder.Sprites))
        {
            Restart(dispatcher, () => _spritesTimer);
        }
        else if (IsIn(path, _folder.Furniture))
        {
            Restart(dispatcher, () => _furnitureTimer);
        }
        else if (IsIn(path, _folder.Pets) && PetRoster.Classify(Path.GetRelativePath(_folder.Pets, path)) is { } change)
        {
            Restart(dispatcher, () =>
            {
                if (!_petTimers.TryGetValue(change, out var timer))
                {
                    timer = OneShot(dispatcher, () => _onPetChanged(change.PetId, change.Kind));
                    _petTimers[change] = timer;
                }

                return timer;
            });
        }
    }

    private static bool IsIn(string path, string folder) =>
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || string.Equals(path, folder, StringComparison.OrdinalIgnoreCase);

    /// <summary>On the UI thread, (re)starts the debounce timer <paramref name="timer"/> gives.</summary>
    private static void Restart(Dispatcher dispatcher, Func<DispatcherTimer> timer) =>
        dispatcher.BeginInvoke(() =>
        {
            var t = timer();
            t.Stop();
            t.Start();
        });
}
