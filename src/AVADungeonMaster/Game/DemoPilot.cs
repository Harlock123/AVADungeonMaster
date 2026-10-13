using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

/// <summary>
/// Plays the attract demo: a ready-made party walks the first level toward squares it hasn't stood on,
/// opening doors, picking things up, and turning to fight whatever comes.
/// </summary>
public sealed class DemoPilot(GameEngine game)
{
    private readonly HashSet<(int, int)> _been = [];
    private double _next = 0.6;
    public double Elapsed { get; private set; }

    public void Update(double dt)
    {
        Elapsed += dt;
        var w = game.World;
        _been.Add((w.X, w.Y));
        _next -= dt;
        if (_next > 0) return;
        _next = 0.22;
        if (game.Top is { } p)
        {
            int pick = p.Options.FindIndex(o => o.Enabled && o.Act is not null);
            if (pick >= 0) { p.Selected = pick; game.Input("Enter"); } else game.Input("Esc");
            _next = 1.0;
            return;
        }
        // A creature beside us: face it and fight.
        foreach (Dir d in Enum.GetValues<Dir>())
        {
            var (dx, dy) = d.Step();
            if (game.CreatureAt(w.X + dx, w.Y + dy) is null) continue;
            if (d != w.Facing)
            {
                game.Input(d == w.Facing.Left() ? "Left" : "Right");
                return;
            }
            game.Input("Z");
            game.Input("X");
            return;
        }
        if (w.State.ThingsAt(w.X, w.Y).Count > 0 && w.Party.Any(c => c.Alive && c.Pack.Count < 8))
        {
            game.Input("G");
            return;
        }
        var step = NextStep();
        if (step is not { } to)
        {
            game.Input("Right");
            return;
        }
        var want = to.X > w.X ? Dir.East : to.X < w.X ? Dir.West : to.Y > w.Y ? Dir.South : Dir.North;
        if (want != w.Facing)
        {
            game.Input(want == w.Facing.Right() ? "Right" : "Left");
            return;
        }
        if (w.Here.At(to.X, to.Y) == Cell.Door && !w.IsOpenDoor(to.X, to.Y)) game.Input("Space");
        else game.Input("Up");
    }

    /// <summary>The first step toward the nearest square not yet stood on (through doors, avoiding pits and stairs).</summary>
    private (int X, int Y)? NextStep()
    {
        var w = game.World;
        var start = (w.X, w.Y);
        var prev = new Dictionary<(int, int), (int, int)> { [start] = start };
        var q = new Queue<(int, int)>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                var n = (x + dx, y + dy);
                if (prev.ContainsKey(n)) continue;
                var c = w.Here.At(n.Item1, n.Item2);
                bool door = c == Cell.Door;
                if (!door && !w.Walkable(n.Item1, n.Item2)) continue;
                if (c is Cell.Pit or Cell.StairsUp or Cell.StairsDown or Cell.Teleporter) continue;
                prev[n] = (x, y);
                if (!_been.Contains(n))
                {
                    var at = n;
                    while (prev[at] != start) at = prev[at];
                    return at;
                }
                q.Enqueue(n);
            }
        }
        return null;
    }
}
