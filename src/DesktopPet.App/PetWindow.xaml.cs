using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Math;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>Sprite-sized transparent window. Dumb view: it shows whatever <see cref="Present"/> tells it.</summary>
public partial class PetWindow : Window
{
    private static readonly TimeSpan TopmostInterval = TimeSpan.FromSeconds(2);

    private readonly Stopwatch _sinceTopmost = Stopwatch.StartNew();
    private nint _hwnd;
    private RectI _bounds;
    private BitmapSource? _frame;
    private bool _facingLeft;

    public PetWindow()
    {
        InitializeComponent();
    }

    /// <summary>Creates the native window and applies pet styles before it is ever shown.</summary>
    public void CreateHandle()
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        PetWindowInterop.ApplyPetStyles(_hwnd);
    }

    /// <summary>Updates position (physical pixels), sprite frame and facing; only touches what changed.</summary>
    public void Present(RectI bounds, BitmapSource frame, bool facingLeft)
    {
        if (bounds != _bounds && _hwnd != 0)
        {
            _bounds = bounds;
            PetWindowInterop.SetBounds(_hwnd, bounds);
        }

        if (!ReferenceEquals(frame, _frame))
        {
            _frame = frame;
            Sprite.Source = frame;
        }

        if (facingLeft != _facingLeft)
        {
            _facingLeft = facingLeft;
            Flip.ScaleX = facingLeft ? -1 : 1;
        }

        if (_sinceTopmost.Elapsed >= TopmostInterval && _hwnd != 0)
        {
            _sinceTopmost.Restart();
            PetWindowInterop.BringToTopmost(_hwnd);
        }
    }
}
