using Deskling.Core.Math;

namespace Deskling.Core.World;

/// <summary>A visible top-level window, in physical pixels.</summary>
/// <param name="Id">Opaque stable identity (the HWND value on Windows).</param>
/// <param name="Bounds">Visible frame (DWM extended frame bounds, without invisible resize borders).</param>
/// <param name="IsMaximized">Maximized windows are not platforms (their top is the top of the screen).</param>
public sealed record WindowInfo(long Id, RectI Bounds, bool IsMaximized = false);
