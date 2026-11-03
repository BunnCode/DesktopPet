using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DesktopPet.Core.Math;
using DesktopPet.Platform.Windows;

namespace DesktopPet.App;

/// <summary>
/// Sprite-sized transparent window. Dumb view: it shows whatever <see cref="Present"/> tells it and reports
/// left-button gestures. Windows only delivers clicks on visible pixels (layered window), so every press is "on the pet".
/// </summary>
public partial class PetWindow : Window
{
    private static readonly TimeSpan TopmostInterval = TimeSpan.FromSeconds(2);

    private readonly Stopwatch _sinceTopmost = Stopwatch.StartNew();
    private nint _hwnd;
    private RectI _bounds;
    private BitmapSource? _frame;
    private bool _facingLeft;
    private bool _pressed;
    private bool _releasingCapture;

    public PetWindow()
    {
        InitializeComponent();
    }

    /// <summary>Left button went down on the pet. The mouse is captured until release.</summary>
    public event EventHandler? PointerPressed;

    /// <summary>Left button released after a press.</summary>
    public event EventHandler? PointerReleased;

    /// <summary>Capture was taken away mid-gesture (e.g. Alt-Tab, another window grabbed the mouse).</summary>
    public event EventHandler? PointerCanceled;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);
        _pressed = true;
        CaptureMouse(); // keep receiving input while the cursor outruns the window
        PointerPressed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonUp(e);

        // Track our own press flag rather than IsMouseCaptured, so a release is never lost even if capture failed.
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        PointerReleased?.Invoke(this, EventArgs.Empty);
        _releasingCapture = true;
        ReleaseMouseCapture();
        _releasingCapture = false;
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_pressed && !_releasingCapture)
        {
            _pressed = false;
            PointerCanceled?.Invoke(this, EventArgs.Empty);
        }
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
