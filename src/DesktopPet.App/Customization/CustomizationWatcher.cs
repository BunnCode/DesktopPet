using System.IO;
using System.Windows.Threading;

namespace DesktopPet.App.Customization;

/// <summary>
/// Watches the data folder for edits to settings.json and anything under sprites\, and reports them on the UI thread,
/// debounced (editors often write a file several times in a row, and art tools save in steps).
/// Our own writes (the save, problems.txt, the template) are ignored.
/// </summary>
internal sealed class CustomizationWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private readonly DataFolder _folder;
    private readonly FileSystemWatcher _watcher;
    private readonly DispatcherTimer _settingsTimer;
    private readonly DispatcherTimer _spritesTimer;

    public CustomizationWatcher(DataFolder folder, Action onSettingsChanged, Action onSpritesChanged)
    {
        _folder = folder;
        var dispatcher = Dispatcher.CurrentDispatcher;
        _settingsTimer = OneShot(dispatcher, onSettingsChanged);
        _spritesTimer = OneShot(dispatcher, onSpritesChanged);

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
        DispatcherTimer? timer = null;
        if (string.Equals(path, _folder.Settings, StringComparison.OrdinalIgnoreCase))
        {
            timer = _settingsTimer;
        }
        else if (path.StartsWith(_folder.Sprites, StringComparison.OrdinalIgnoreCase))
        {
            timer = _spritesTimer;
        }

        if (timer is not null)
        {
            dispatcher.BeginInvoke(() =>
            {
                timer.Stop();
                timer.Start();
            });
        }
    }
}
