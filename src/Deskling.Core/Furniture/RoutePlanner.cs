using Deskling.Core.World;

namespace Deskling.Core.Furniture;

/// <summary>One move on the way to furniture.</summary>
internal enum RouteMove
{
    /// <summary>Walk to X on this surface: the destination.</summary>
    Arrive,

    /// <summary>Walk to X, just past the end of this surface, and step onto the next one or drop down.</summary>
    WalkOff,

    /// <summary>Walk to X, then climb up the wall and over onto the surface at its top.</summary>
    ClimbUp,

    /// <summary>Walk to X at the end of this window top, then climb down the window's side and let go.</summary>
    ClimbDown,
}

/// <param name="Move">What to do.</param>
/// <param name="X">Where to walk to first (feet x).</param>
/// <param name="Direction">For <see cref="RouteMove.WalkOff"/>: which way to keep walking (+1 right, -1 left).</param>
/// <param name="WallKey">For climbs: the wall's key.</param>
internal readonly record struct RouteStep(RouteMove Move, double X, int Direction = 0, string? WallKey = null);

/// <summary>What the planner needs to know about the pet.</summary>
/// <param name="HalfWidth">Half its body width (wall contact), px.</param>
/// <param name="Size">Its sprite size, px.</param>
/// <param name="Scale">DPI scale (for the push off a wall when letting go).</param>
/// <param name="CanClimb">Climbing is switched on.</param>
/// <param name="Gravity">Gravity, px/s², to estimate the drift of a drop.</param>
internal readonly record struct Walker(double HalfWidth, double Size, double Scale, bool CanClimb, double Gravity);

/// <summary>
/// Finds a way from where the pet stands to a spot on another surface, using the moves the pet already makes:
/// walking along a surface, stepping onto a neighbour at the same height, dropping off an end, climbing up a wall and
/// over onto the surface at its top, and climbing down a window's side. Cheapest by distance (climbing counts double,
/// it's slower than walking). No route means the pet doesn't go.
/// </summary>
internal static class RoutePlanner
{
    private const double ClimbCost = 2;
    private const double DropCost = 0.25;
    private static readonly int[] Directions = [-1, 1];

    /// <summary>The speed a pet pushes off a wall at when it lets go, DIP/s (see <c>WallBehavior.LetGo</c>).</summary>
    private const double LetGoSpeedDip = 120;

    /// <param name="platforms">Every surface.</param>
    /// <param name="walls">Every wall.</param>
    /// <param name="monitors">The monitors (walkability of ends).</param>
    /// <param name="groundKey">The surface the pet stands on.</param>
    /// <param name="feetX">Where on it.</param>
    /// <param name="targetKey">The surface to get to.</param>
    /// <param name="targetX">Where on it.</param>
    /// <param name="walker">The pet.</param>
    /// <returns>The moves in order, the last one <see cref="RouteMove.Arrive"/>; null if there's no way.</returns>
    public static IReadOnlyList<RouteStep>? Plan(
        IReadOnlyList<Platform> platforms, IReadOnlyList<Wall> walls, IReadOnlyList<MonitorInfo> monitors,
        string groundKey, double feetX, string targetKey, double targetX, Walker walker)
    {
        var start = IndexOf(platforms, groundKey, feetX);
        if (start < 0)
        {
            return null;
        }

        var n = platforms.Count;
        var cost = new double[n];
        var entry = new double[n];
        var from = new (int Node, RouteStep Step)[n];
        var done = new bool[n];
        Array.Fill(cost, double.PositiveInfinity);
        cost[start] = 0;
        entry[start] = feetX;
        from[start] = (-1, default);

        var best = double.PositiveInfinity;
        var bestNode = -1;
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var u, out var d))
        {
            if (done[u] || d > cost[u])
            {
                continue;
            }

            if (d >= best)
            {
                break;
            }

            done[u] = true;
            var s = platforms[u];
            if (s.Key == targetKey && s.SpansX(targetX))
            {
                var total = d + System.Math.Abs(targetX - entry[u]);
                if (total < best)
                {
                    best = total;
                    bestNode = u;
                }
            }

            foreach (var (v, entryX, step, edgeCost) in Moves(platforms, walls, monitors, u, entry[u], walker))
            {
                var nd = d + edgeCost;
                if (!done[v] && nd < cost[v])
                {
                    cost[v] = nd;
                    entry[v] = entryX;
                    from[v] = (u, step);
                    queue.Enqueue(v, nd);
                }
            }
        }

        if (bestNode < 0)
        {
            return null;
        }

        var steps = new List<RouteStep> { new(RouteMove.Arrive, targetX) };
        for (var node = bestNode; from[node].Node >= 0; node = from[node].Node)
        {
            steps.Add(from[node].Step);
        }

        steps.Reverse();
        return steps;
    }

    /// <summary>The ground segment of surface <paramref name="key"/> under <paramref name="x"/>, or -1.</summary>
    public static int IndexOf(IReadOnlyList<Platform> platforms, string key, double x)
    {
        for (var i = 0; i < platforms.Count; i++)
        {
            if (platforms[i].IsGround && platforms[i].Key == key && platforms[i].SpansX(x))
            {
                return i;
            }
        }

        return -1;
    }

    private static IEnumerable<(int Node, double EntryX, RouteStep Step, double Cost)> Moves(
        IReadOnlyList<Platform> platforms, IReadOnlyList<Wall> walls, IReadOnlyList<MonitorInfo> monitors, int u, double entryX, Walker walker)
    {
        var s = platforms[u];

        // Off either end: climb down the window's side there, step onto a neighbour, or drop.
        foreach (var dir in Directions)
        {
            var inside = dir > 0 ? s.Right - 0.5 : s.Left + 0.5;
            if (walker.CanClimb && s.Kind == PlatformKind.WindowTop && SideAt(walls, s, dir) is { } side)
            {
                var releaseX = side.X + (dir * walker.Size / 2);
                if (DropLanding(platforms, releaseX, side.Bottom, -1, dir, walker) is { } landed)
                {
                    var climb = System.Math.Max(0, side.Bottom - s.Y) * ClimbCost;
                    yield return (landed.Node, landed.X, new RouteStep(RouteMove.ClimbDown, inside, dir, side.Key),
                        System.Math.Abs(inside - entryX) + climb + DropCost);
                    continue;
                }
            }

            var past = dir > 0 ? s.Right + 0.5 : s.Left - 0.5;
            if (!WorldQuery.IsWalkable(monitors, past + (dir * walker.HalfWidth), s.Y))
            {
                continue; // a screen edge: the pet turns here
            }

            var next = Neighbour(platforms, u, past) ?? DropLanding(platforms, past, s.Y, u, 0, walker);
            if (next is { } target)
            {
                yield return (target.Node, target.X, new RouteStep(RouteMove.WalkOff, past, dir),
                    System.Math.Abs(past - entryX) + (target.Node == u ? 0 : DropCost));
            }
        }

        if (!walker.CanClimb)
        {
            yield break;
        }

        // Up a wall that rises beside this surface, and over onto whatever is at its top.
        var midY = s.Y - (walker.Size / 2);
        foreach (var w in walls)
        {
            var approach = w.X - (w.TowardWall * walker.HalfWidth);
            if (!s.SpansX(approach) || !w.SpansY(midY) || w.Bottom < s.Y - 2 || w.Top > s.Y - (walker.Size * 0.75))
            {
                continue;
            }

            var overX = w.X + (w.TowardWall * (walker.HalfWidth + 2));
            for (var i = 0; i < platforms.Count; i++)
            {
                var p = platforms[i];
                if (i != u && p.IsGround && p.SpansX(overX) && System.Math.Abs(p.Y - w.Top) <= 2)
                {
                    yield return (i, overX, new RouteStep(RouteMove.ClimbUp, approach, w.TowardWall, w.Key),
                        System.Math.Abs(approach - entryX) + ((s.Y - w.Top) * ClimbCost));
                    break;
                }
            }
        }
    }

    /// <summary>The outside face of a window top's side at one end, starting at the top (to climb down).</summary>
    internal static Wall? SideAt(IReadOnlyList<Wall> walls, Platform top, int dir)
    {
        var edgeX = dir > 0 ? top.Right : top.Left;
        foreach (var w in walls)
        {
            if (w.Kind == WallKind.WindowSide && w.TowardWall == -dir
                && System.Math.Abs(w.X - edgeX) <= 2 && System.Math.Abs(w.Top - top.Y) <= 2)
            {
                return w;
            }
        }

        return null;
    }

    /// <summary>A different segment at the same height that continues where this one ends (a monitor seam).</summary>
    private static (int Node, double X)? Neighbour(IReadOnlyList<Platform> platforms, int u, double x)
    {
        var y = platforms[u].Y;
        for (var i = 0; i < platforms.Count; i++)
        {
            if (i != u && platforms[i].IsGround && platforms[i].SpansX(x) && System.Math.Abs(platforms[i].Y - y) <= 1)
            {
                return (i, x);
            }
        }

        return null;
    }

    /// <summary>
    /// Where a pet falling from (<paramref name="x"/>, <paramref name="y"/>) lands: the highest surface below that
    /// spans its x, allowing for a push off a wall (<paramref name="pushDir"/>, 0 for none).
    /// </summary>
    private static (int Node, double X)? DropLanding(
        IReadOnlyList<Platform> platforms, double x, double y, int exclude, int pushDir, Walker walker)
    {
        var landing = Highest(platforms, x, y, exclude);
        if (landing < 0 || pushDir == 0)
        {
            return landing < 0 ? null : (landing, x);
        }

        // Pushed off the wall: it drifts while it falls. Estimate the drift from the fall time and look again.
        var fall = System.Math.Max(0, platforms[landing].Y - y);
        var drift = pushDir * LetGoSpeedDip * walker.Scale * System.Math.Sqrt(2 * fall / System.Math.Max(1, walker.Gravity));
        var drifted = Highest(platforms, x + drift, y, exclude);
        return drifted < 0 ? null : (drifted, x + drift);
    }

    private static int Highest(IReadOnlyList<Platform> platforms, double x, double y, int exclude)
    {
        var best = -1;
        for (var i = 0; i < platforms.Count; i++)
        {
            var p = platforms[i];
            if (i != exclude && p.IsGround && p.Y >= y - 0.5 && p.SpansX(x) && (best < 0 || p.Y < platforms[best].Y))
            {
                best = i;
            }
        }

        return best;
    }
}
