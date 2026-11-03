using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopPet.Core.Math;
using DesktopPet.Core.Physics;
using DesktopPet.Core.Simulation;
using DesktopPet.Core.World;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.UI;

/// <summary>What the overlay draws; a plain copy so the overlay never touches the simulation directly.</summary>
/// <remarks>
/// <c>Core.World.Platform</c> is spelled out: inside <c>DesktopPet.App</c>, a bare <c>Platform</c> resolves to the
/// <c>DesktopPet.Platform</c> namespace before using-directives are considered.
/// </remarks>
internal sealed record DebugView(
    IReadOnlyList<Core.World.Platform> Platforms, IReadOnlyList<Wall> Walls, IReadOnlyList<DebugPet> Pets, int WindowCount)
{
    /// <summary>Surfaces some pet is standing on, hanging from or clinging to (highlighted).</summary>
    public HashSet<string> InUse { get; } = [.. Pets.Select(p => p.GroundKey).OfType<string>()];
}

/// <summary>One pet's box and state for the overlay.</summary>
internal sealed record DebugPet(string Name, RectI Bounds, string State, BodyMode Mode, string? GroundKey);

/// <summary>
/// Click-through overlay showing what Core "sees": floors (green), window tops (orange), hang bars (dashed magenta),
/// climbable window sides (blue) and screen edges (dotted blue), the surface the pet is on (yellow), the pet's sprite
/// box and its state. One window per monitor so each renders at its own DPI.
/// </summary>
internal sealed class DebugOverlay : IDisposable
{
    private readonly List<(MonitorInfo Monitor, OverlayWindow Window)> _windows = [];
    private IReadOnlyList<MonitorInfo> _builtFor = [];
    private bool _rebuilding;

    public void Update(IReadOnlyList<MonitorInfo> monitors, DebugView view)
    {
        // Creating/positioning windows can re-enter here (WPF renders synchronously on resize); never nest a rebuild.
        if (_rebuilding)
        {
            return;
        }

        if (!_builtFor.SequenceEqual(monitors))
        {
            Rebuild(monitors);
        }

        foreach (var (monitor, window) in _windows)
        {
            window.Canvas.Show(monitor, view);
        }
    }

    public void Dispose()
    {
        foreach (var (_, window) in _windows)
        {
            window.Close();
        }

        _windows.Clear();
        _builtFor = [];
    }

    private void Rebuild(IReadOnlyList<MonitorInfo> monitors)
    {
        _rebuilding = true;
        try
        {
            Dispose();
            _builtFor = monitors;
            foreach (var monitor in monitors)
            {
                var window = new OverlayWindow();
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                PetWindowInterop.ApplyOverlayStyles(hwnd);
                PetWindowInterop.SetBounds(hwnd, monitor.Bounds);
                window.Show();
                _windows.Add((monitor, window));
            }
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private sealed class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            IsHitTestVisible = false;
            Title = "DesktopPet debug overlay";
            Content = Canvas;
        }

        public OverlayCanvas Canvas { get; } = new();
    }

    private sealed class OverlayCanvas : FrameworkElement
    {
        private static readonly Pen FloorPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 60, 220, 90)), 3));
        private static readonly Pen WindowPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 150, 30)), 3));
        private static readonly Pen HangPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 230, 80, 230)), 3) { DashStyle = DashStyles.Dash });
        private static readonly Pen WallPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 80, 170, 255)), 3));
        private static readonly Pen EdgePen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(160, 80, 170, 255)), 3) { DashStyle = DashStyles.Dot });
        private static readonly Pen GroundPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(255, 255, 235, 40)), 5));
        private static readonly Pen PetPen = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(220, 40, 220, 255)), 1.5));
        private static readonly Brush LabelBackground = Frozen(new SolidColorBrush(Color.FromArgb(190, 20, 20, 20)));
        private static readonly Typeface Mono = new("Consolas");

        private MonitorInfo? _monitor;
        private DebugView? _view;

        public void Show(MonitorInfo monitor, DebugView view)
        {
            _monitor = monitor;
            _view = view;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_monitor is not { } m || _view is not { } view)
            {
                return;
            }

            var bounds = m.Bounds;
            Point ToDip(double x, double y) => new((x - bounds.Left) / m.Scale, (y - bounds.Top) / m.Scale);

            foreach (var p in view.Platforms)
            {
                if (p.Y < bounds.Top || p.Y > bounds.Bottom || p.Right <= bounds.Left || p.Left >= bounds.Right)
                {
                    continue;
                }

                var pen = view.InUse.Contains(p.Key) ? GroundPen
                    : p.Kind switch { PlatformKind.Floor => FloorPen, PlatformKind.HangBar => HangPen, _ => WindowPen };
                var y = p.Kind == PlatformKind.HangBar ? p.Y + 1.5 : p.Y - 1.5; // draw on the open side of the edge
                dc.DrawLine(pen, ToDip(System.Math.Max(p.Left, bounds.Left), y), ToDip(System.Math.Min(p.Right, bounds.Right), y));
            }

            foreach (var w in view.Walls)
            {
                if (w.X < bounds.Left || w.X > bounds.Right || w.Bottom <= bounds.Top || w.Top >= bounds.Bottom)
                {
                    continue;
                }

                var pen = view.InUse.Contains(w.Key) ? GroundPen : w.Kind == WallKind.ScreenEdge ? EdgePen : WallPen;
                var x = w.X - (w.TowardWall * 1.5); // on the side the pet clings
                dc.DrawLine(
                    pen, ToDip(x, System.Math.Max(w.Top, bounds.Top)), ToDip(x, System.Math.Min(w.Bottom, bounds.Bottom)));
            }

            foreach (var pet in view.Pets)
            {
                if (!pet.Bounds.Intersects(bounds))
                {
                    continue;
                }

                var topLeft = ToDip(pet.Bounds.Left, pet.Bounds.Top);
                var bottomRight = ToDip(pet.Bounds.Right, pet.Bounds.Bottom);
                dc.DrawRectangle(null, PetPen, new Rect(topLeft, bottomRight));
                DrawLabel(dc, $"{pet.Name}: {pet.State} · {pet.Mode}", new Point(topLeft.X, topLeft.Y - 18));
            }

            if (m.IsPrimary)
            {
                var floors = view.Platforms.Count(p => p.Kind == PlatformKind.Floor);
                var bars = view.Platforms.Count(p => p.Kind == PlatformKind.HangBar);
                var tops = view.Platforms.Count - floors - bars;
                DrawLabel(
                    dc,
                    $"DesktopPet debug · {view.Pets.Count} pets · {view.WindowCount} windows · {tops} window tops"
                    + $" · {view.Walls.Count} walls · {bars} hang bars · {floors} floors",
                    ToDip(m.WorkArea.Left + 8, m.WorkArea.Top + 8));
            }
        }

        private void DrawLabel(DrawingContext dc, string text, Point at)
        {
            var formatted = new FormattedText(
                text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Mono, 12, Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRoundedRectangle(LabelBackground, null, new Rect(at.X - 4, at.Y - 2, formatted.Width + 8, formatted.Height + 4), 3, 3);
            dc.DrawText(formatted, at);
        }

        private static T Frozen<T>(T freezable)
            where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
