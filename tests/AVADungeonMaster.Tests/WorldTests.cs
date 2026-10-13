using AVADungeonMaster.Game;
using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Tests;

public class WorldTests
{
    public static TheoryData<int> LevelNumbers => new(Levels.All.Select(l => l.Number));

    [Theory]
    [MemberData(nameof(LevelNumbers))]
    public void Every_level_is_a_closed_16x16_map(int n)
    {
        var lv = Levels.Get(n);
        Assert.Equal(16, lv.Map.Length);
        Assert.All(lv.Map, r => Assert.Equal(16, r.Length));
        for (int i = 0; i < 16; i++)
            foreach (var (x, y) in new[] { (i, 0), (i, 15), (0, i), (15, i) })
                Assert.True(Level.IsWall(lv.At(x, y)), $"{lv.Name}: border at {x},{y}");
    }

    [Theory]
    [MemberData(nameof(LevelNumbers))]
    public void Everything_placed_on_a_level_is_real_and_where_it_can_be(int n)
    {
        var lv = Levels.Get(n);
        foreach (var (x, y, id) in lv.Items)
        {
            Assert.True(Items.Exists(id), id);
            Assert.True(lv.At(x, y) is Cell.Floor or Cell.Plate, $"{lv.Name}: {id} at {x},{y} is in a {lv.At(x, y)}");
        }
        foreach (var (x, y, kind) in lv.Monsters)
        {
            Assert.NotNull(MonsterKind.Get(kind));
            Assert.Equal(Cell.Floor, lv.At(x, y));
        }
        foreach (var ((x, y), key) in lv.Locks)
        {
            Assert.Equal(Cell.Locked, lv.At(x, y));
            Assert.Equal(ItemKind.Key, Items.Get(key).Kind);
        }
        foreach (var ((x, y), targets) in lv.Triggers)
        {
            Assert.True(lv.At(x, y) is Cell.Plate or Cell.Button);
            Assert.All(targets, t => Assert.Equal(Cell.Gate, lv.At(t.X, t.Y)));
        }
        foreach (var ((x, y), to) in lv.Teleports)
        {
            Assert.Equal(Cell.Teleporter, lv.At(x, y));
            Assert.Equal(Cell.Floor, lv.At(to.X, to.Y));
        }
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                if (lv.At(x, y) == Cell.Writing) Assert.True(lv.Writing.ContainsKey((x, y)), $"{lv.Name}: no words at {x},{y}");
                if (lv.At(x, y) == Cell.Locked) Assert.True(lv.Locks.ContainsKey((x, y)), $"{lv.Name}: no key for {x},{y}");
                if (lv.At(x, y) == Cell.Gate) Assert.Contains(lv.Triggers.Values, t => t.Contains((x, y)));
            }
        Assert.Equal(n == 0 ? 0 : 1, lv.Map.Sum(r => r.Count(ch => ch == '<')));
        Assert.Equal(n == Levels.Deepest ? 0 : 1, lv.Map.Sum(r => r.Count(ch => ch == '>')));
    }

    /// <summary>
    /// Walks every level as a player could: keys picked up open their locks, plates and buttons raise
    /// their gates, teleporters carry you. Every open square must be reachable, the way down must be,
    /// and on each level from the first the way down must need something (a key or a switch).
    /// </summary>
    [Fact]
    public void Every_level_can_be_solved_and_each_needs_its_key_or_switch()
    {
        var keys = new HashSet<string>();
        foreach (var lv in Levels.All)
        {
            var start = lv.Start ?? lv.Find('<');
            var (seen, found) = Explore(lv, start, keys, useKeys: true);
            keys.UnionWith(found);
            var down = lv.Find('>');
            if (down.X >= 0) Assert.True(seen.Contains(down), $"{lv.Name}: the way down can't be reached");
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (!Level.IsWall(lv.At(x, y)) && lv.At(x, y) != Cell.Pit)
                        Assert.True(seen.Contains((x, y)), $"{lv.Name}: {x},{y} can't be reached");
            if (lv.Number >= 1 && down.X >= 0)
            {
                var (bare, _) = Explore(lv, start, [], useKeys: false);
                Assert.False(bare.Contains(down), $"{lv.Name}: the way down needs nothing");
            }
        }
    }

    private static (HashSet<(int, int)> Seen, HashSet<string> Keys) Explore(Level lv, (int X, int Y) start, HashSet<string> held, bool useKeys)
    {
        var keys = new HashSet<string>(held);
        var opened = new HashSet<(int, int)>();
        HashSet<(int, int)> seen;
        while (true)
        {
            seen = [start];
            var stack = new Stack<(int, int)>(seen);
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                if (lv.Teleports.TryGetValue((x, y), out var to))
                {
                    if (seen.Add(to)) stack.Push(to);
                    continue;
                }
                foreach (Dir d in Enum.GetValues<Dir>())
                {
                    var (dx, dy) = d.Step();
                    var n = (x + dx, y + dy);
                    var c = lv.At(n.Item1, n.Item2);
                    if (seen.Contains(n) || Level.IsWall(c) || c == Cell.Pit) continue;
                    if (c == Cell.Locked && !(useKeys && keys.Contains(lv.Locks[n]))) continue;
                    if (c == Cell.Gate && !(useKeys && opened.Contains(n))) continue;
                    if (c == Cell.RuneDoor && !useKeys) continue;
                    seen.Add(n);
                    stack.Push(n);
                }
            }
            bool changed = false;
            foreach (var (x, y, id) in lv.Items)
                if (seen.Contains((x, y)) && Items.Get(id).Kind == ItemKind.Key) changed |= keys.Add(id);
            foreach (var ((tx, ty), targets) in lv.Triggers)
            {
                bool reached = lv.At(tx, ty) == Cell.Plate
                    ? seen.Contains((tx, ty))
                    : Enum.GetValues<Dir>().Any(d => seen.Contains((tx + d.Step().X, ty + d.Step().Y)));
                if (reached) foreach (var t in targets) changed |= opened.Add(t);
            }
            if (!changed || !useKeys) break;
        }
        return (seen, keys);
    }

    [Fact]
    public void The_hall_has_twelve_mirrors_for_twelve_champions()
    {
        var g = new GameEngine(1, Helpers.TempSaves());
        var roster = Roster.Make();
        Assert.Equal(12, roster.Count);
        Assert.Equal(12, roster.Select(c => c.Name).Distinct().Count());
        var hall = Levels.Get(0);
        var mirrors = Enumerable.Range(0, 16).SelectMany(y => Enumerable.Range(0, 16).Select(x => (x, y))).Where(p => hall.At(p.x, p.y) == Cell.Mirror).ToList();
        Assert.Equal(12, mirrors.Count);
        Assert.Equal(Enumerable.Range(0, 12), mirrors.Select(m => g.MirrorIndex(m.x, m.y)).OrderBy(i => i));
        Assert.All(roster, c => Assert.All(c.Everything, t => Assert.True(Items.Exists(t.Id))));
    }

    [Fact]
    public void Every_monster_and_portrait_has_a_drawing()
    {
        foreach (var (name, rows) in AVADungeonMaster.Views.Sprites.All)
        {
            Assert.Equal(16, rows.Length);
            Assert.All(rows, r => Assert.True(r.Length == 16, $"{name}: {r}"));
        }
        foreach (var m in MonsterKind.All) Assert.True(AVADungeonMaster.Views.Sprites.All.ContainsKey(m.Sprite), m.Id);
        foreach (var p in new[] { "warrior", "rogue", "priest", "mage" }) Assert.True(AVADungeonMaster.Views.Sprites.All.ContainsKey(p));
    }

    [Fact]
    public void Every_spell_has_a_scroll_somewhere_or_is_known_from_the_start()
    {
        var placed = Levels.All.SelectMany(l => l.Items).Select(i => i.Id).Concat(Roster.Make().SelectMany(c => c.Everything).Select(t => t.Id)).ToHashSet();
        var g = Helpers.Quick();
        foreach (var s in Spell.All)
        {
            bool scroll = Items.All.Any(i => i.Kind == ItemKind.Scroll && placed.Contains(i.Id) && i.Text.Contains(s.Runes.Split(' ')[0]));
            Assert.True(scroll || g.World.Spellbook.Contains(s.Name), s.Name);
        }
    }

    [Fact]
    public void Every_sound_has_a_file()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(dir, s + ".wav")), s.ToString());
    }
}
