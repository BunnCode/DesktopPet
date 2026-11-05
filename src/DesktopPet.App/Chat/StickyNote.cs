using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DesktopPet.Core.Math;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.Chat;

/// <summary>
/// A sticky note a pet left on another window. It isn't topmost: the host keeps it just above that window in
/// z-order, so whatever covers the window covers the note. Clicking it dismisses it.
/// </summary>
internal sealed class StickyNote : Window
{
    private const double WidthDip = 170;

    private nint _hwnd;
    private (int Left, int Top) _position = (int.MinValue, int.MinValue);

    /// <param name="windowId">The window it's stuck to.</param>
    /// <param name="offset">Where on that window, from its top-left corner (physical pixels).</param>
    /// <param name="text">What it says.</param>
    /// <param name="author">The pet that wrote it.</param>
    /// <param name="fromBottom">The offset is where the note's bottom-left corner goes instead of its top-left.</param>
    public StickyNote(long windowId, Vec2 offset, string text, string author, bool fromBottom = false)
    {
        WindowId = windowId;
        Offset = offset;
        FromBottom = fromBottom;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Cursor = Cursors.Hand;
        Title = "DesktopPet note";
        ToolTip = "Click to dismiss";

        var ink = new SolidColorBrush(Color.FromRgb(0x3B, 0x34, 0x20));
        ink.Freeze();
        var hand = new FontFamily("Segoe Print, Segoe UI");
        var lines = new StackPanel();
        lines.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = hand,
            FontSize = 12,
            Foreground = ink,
        });
        lines.Children.Add(new TextBlock
        {
            Text = "— " + author,
            FontFamily = hand,
            FontSize = 10.5,
            Foreground = ink,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0),
        });

        Content = new Border
        {
            Width = WidthDip,
            Margin = new Thickness(4, 2, 6, 7), // room for the shadow
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xF1, 0x8C)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0xD0, 0x5C)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 7),
            Child = lines,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 2, Opacity = 0.35 },
        };

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            PetWindowInterop.ApplyPetStyles(_hwnd); // no Alt-Tab entry, never takes focus
        };
    }

    /// <summary>Clicked: the host removes it.</summary>
    public event EventHandler? Dismissed;

    public long WindowId { get; }

    public Vec2 Offset { get; }

    /// <summary><see cref="Offset"/> is for the note's bottom edge (a note above a pet's head).</summary>
    public bool FromBottom { get; }

    /// <summary>
    /// Follows its window: hidden while that is minimized or too small for it, otherwise placed at its offset (kept
    /// inside the window) and just above <paramref name="below"/> in z-order: its window, or the note under it when
    /// several share one (each directly above its own window would swap places with the others every time).
    /// </summary>
    /// <returns>Whether it's showing, so the next note on the window can go above it.</returns>
    public bool Track(TrackedWindowState state, RectI window, long below)
    {
        if (state != TrackedWindowState.Shown)
        {
            HideNote();
            return false;
        }

        if (!IsVisible)
        {
            Show();
            UpdateLayout();
            _position = (int.MinValue, int.MinValue);
        }

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = ActualWidth * scale;
        var height = ActualHeight * scale;
        if (width > window.Width || height > window.Height)
        {
            HideNote();
            return false;
        }

        var left = System.Math.Clamp(window.Left + Offset.X, window.Left, window.Right - width);
        var top = System.Math.Clamp(window.Top + Offset.Y - (FromBottom ? height : 0), window.Top, window.Bottom - height);
        var position = ((int)System.Math.Round(left), (int)System.Math.Round(top));
        if (position != _position)
        {
            _position = position;
            PetWindowInterop.SetPosition(_hwnd, position.Item1, position.Item2);
        }

        WindowTracker.PlaceAbove(_hwnd, below);
        return true;
    }

    /// <summary>Its window handle, for the note stacked above it.</summary>
    public long Handle => _hwnd;

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void HideNote()
    {
        if (IsVisible)
        {
            Hide();
        }
    }
}
