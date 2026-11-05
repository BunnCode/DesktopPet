namespace Deskling.Core.Simulation;

/// <summary>
/// A request from the simulation to move a real window by (<paramref name="Dx"/>, <paramref name="Dy"/>) physical pixels.
/// Core never touches the OS; the host carries these out (and Core notices if the window didn't move).
/// </summary>
public readonly record struct WindowCommand(long WindowId, int Dx, int Dy);
