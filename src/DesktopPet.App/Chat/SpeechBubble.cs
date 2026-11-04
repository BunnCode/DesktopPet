using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopPet.Core.Math;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App.Chat;

/// <summary>
/// A speech bubble above a pet: click-through, topmost, sized to its text. The host moves it with the pet each frame
/// (<see cref="Follow"/>) and it fades out on its own after the time it was given.
/// </summary>
internal sealed class SpeechBubble : Window
{
    private const double MaxTextWidthDip = 220;
    private const double FadeSeconds = 0.4;
    private const double GapDip = 2;
    private const double TailHalfWidthDip = 6;

    private readonly TextBlock _text;
    private readonly Path _tail;
    private nint _hwnd;
    private double _shownFor;
    private double _seconds;
    private (int Left, int Top) _position = (int.MinValue, int.MinValue);

    public SpeechBubble()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        Title = "DesktopPet speech";

        var ink = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3A));
        ink.Freeze();
        _text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = MaxTextWidthDip,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12.5,
            Foreground = ink,
        };

        var body = new Border
        {
            Background = Brushes.White,
            BorderBrush = ink,
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 5, 9, 6),
            Child = _text,
        };

        // A little triangle under the bubble, pointing at the pet's head; it overlaps the border to hide the seam.
        _tail = new Path
        {
            Data = Geometry.Parse($"M 0,0 L {TailHalfWidthDip},8 L {2 * TailHalfWidthDip},0"),
            Fill = Brushes.White,
            Stroke = ink,
            StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, -1.5, 0, 0),
        };

        var stack = new StackPanel { Margin = new Thickness(2) };
        stack.Children.Add(body);
        stack.Children.Add(_tail);
        Content = stack;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            PetWindowInterop.ApplyOverlayStyles(_hwnd); // clicks go through to whatever is behind
        };
    }

    /// <summary>Whether a line is showing.</summary>
    public bool Speaking => IsVisible;

    /// <summary>Shows <paramref name="text"/> for <paramref name="seconds"/> (replacing any line still showing).</summary>
    public void Say(string text, double seconds)
    {
        _text.Text = text;
        _seconds = seconds;
        _shownFor = 0;
        Opacity = 1;
        _position = (int.MinValue, int.MinValue);
        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();
    }

    /// <summary>
    /// Advances the fade and moves the bubble so its tail points at <paramref name="head"/> (the top centre of the
    /// pet's sprite, physical pixels), kept inside <paramref name="area"/> and raised above any of
    /// <paramref name="taken"/> (bubbles placed earlier this frame) it would cover. Returns where it went, or null
    /// when it isn't showing.
    /// </summary>
    public RectI? Follow(double dt, Vec2 head, RectI area, IReadOnlyList<RectI> taken)
    {
        if (!IsVisible || _hwnd == 0)
        {
            return null;
        }

        _shownFor += dt;
        if (_shownFor >= _seconds)
        {
            Hide();
            return null;
        }

        Opacity = System.Math.Clamp((_seconds - _shownFor) / FadeSeconds, 0, 1);

        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var width = (int)System.Math.Round(ActualWidth * scale);
        var height = (int)System.Math.Round(ActualHeight * scale);
        var left = (int)System.Math.Round(head.X - (width / 2.0));
        left = System.Math.Clamp(left, area.Left, System.Math.Max(area.Left, area.Right - width));
        var top = (int)System.Math.Round(head.Y - height - (GapDip * scale));

        // Pets side by side: stack above the bubbles already placed instead of covering them.
        for (var moved = true; moved;)
        {
            moved = false;
            foreach (var other in taken)
            {
                if (left < other.Right && other.Left < left + width && top < other.Bottom && other.Top < top + height)
                {
                    top = other.Top - height;
                    moved = true;
                }
            }
        }

        top = System.Math.Max(area.Top, top);

        // The tail stays over the pet even when the bubble is pushed sideways by the screen edge.
        var tailDip = ((head.X - left) / scale) - TailHalfWidthDip - 2;
        var tailMax = System.Math.Max(8, (ActualWidth - (2 * TailHalfWidthDip)) - 12);
        _tail.Margin = new Thickness(System.Math.Clamp(tailDip, 8, tailMax), -1.5, 0, 0);

        if ((left, top) != _position)
        {
            _position = (left, top);
            PetWindowInterop.SetPosition(_hwnd, left, top);
        }

        return new RectI(left, top, left + width, top + height);
    }

    /// <summary>Hides the bubble now (chat switched off).</summary>
    public void Silence()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    /// <summary>Keeps the bubble above the pets (both are topmost; the one raised last wins).</summary>
    public void BringToFront()
    {
        if (_hwnd != 0 && IsVisible)
        {
            PetWindowInterop.BringToTopmost(_hwnd);
        }
    }
}
