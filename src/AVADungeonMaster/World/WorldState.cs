using System.Text.Json;
using System.Text.Json.Serialization;
using AVADungeonMaster.Rules;

namespace AVADungeonMaster.World;

/// <summary>A creature in the dungeon, where it is and how it's doing.</summary>
public sealed class Creature
{
    public string Kind { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Health { get; set; }
    public double MoveTimer { get; set; }
    public double AttackTimer { get; set; }
    /// <summary>Held helpless (the Starheart's binding) for this many seconds.</summary>
    public double Bound { get; set; }
    [JsonIgnore] public double Flash { get; set; }
    [JsonIgnore] public MonsterKind Info => MonsterKind.Get(Kind);
}

/// <summary>What has changed on a level since it was made: doors, things lying about, creatures.</summary>
public sealed class LevelState
{
    public HashSet<string> Open { get; set; } = [];
    public HashSet<string> Fired { get; set; } = [];
    public Dictionary<string, List<Thing>> Floor { get; set; } = [];
    public List<Creature> Creatures { get; set; } = [];
    public bool Visited { get; set; }

    public static string Key(int x, int y) => $"{x},{y}";

    public List<Thing> ThingsAt(int x, int y) => Floor.TryGetValue(Key(x, y), out var l) ? l : [];

    public void Put(int x, int y, Thing t)
    {
        if (!Floor.TryGetValue(Key(x, y), out var l)) Floor[Key(x, y)] = l = [];
        l.Add(t);
    }
}

/// <summary>Everything a saved game holds.</summary>
public sealed class WorldState
{
    public List<Champion> Party { get; set; } = [];
    /// <summary>Which level the party is on: 0 is the Hall of Mirrors.</summary>
    public int Depth { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Dir Facing { get; set; }
    /// <summary>Seconds of play.</summary>
    public double Time { get; set; }
    /// <summary>Seconds of magical light left, and how bright it is.</summary>
    public double MagicLight { get; set; }
    public double MagicLightPower { get; set; }
    public double Ward { get; set; }
    public Dictionary<int, LevelState> Visits { get; set; } = [];
    /// <summary>Spells learned, by name.</summary>
    public List<string> Spellbook { get; set; } = [];
    /// <summary>Which mirrors' champions have been taken.</summary>
    public HashSet<int> Freed { get; set; } = [];
    public int Kills { get; set; }
    public bool Won { get; set; }
    public ulong Dice { get; set; } = 1;
    public DateTime Saved { get; set; }

    [JsonIgnore] public Level Here => Levels.Get(Depth);
    [JsonIgnore] public LevelState State => StateOf(Depth);

    public LevelState StateOf(int n)
    {
        if (Visits.TryGetValue(n, out var s)) return s;
        // First visit: lay the level out as designed.
        var lv = World.Levels.Get(n);
        s = new LevelState();
        foreach (var (x, y, id) in lv.Items) s.Put(x, y, new Thing(id));
        foreach (var (x, y, kind) in lv.Monsters)
            s.Creatures.Add(new Creature { Kind = kind, X = x, Y = y, Health = MonsterKind.Get(kind).Health, MoveTimer = 1 + x * 0.1 });
        Visits[n] = s;
        return s;
    }

    /// <summary>A square as it is now: doors opened, gates raised.</summary>
    public Cell CellAt(int x, int y)
    {
        var c = Here.At(x, y);
        return Level.IsDoor(c) && State.Open.Contains(LevelState.Key(x, y)) ? Cell.Floor : c;
    }

    public bool IsOpenDoor(int x, int y) => Level.IsDoor(Here.At(x, y)) && State.Open.Contains(LevelState.Key(x, y));

    /// <summary>Whether the party (or a creature) could stand there.</summary>
    public bool Walkable(int x, int y)
    {
        var c = CellAt(x, y);
        return !Level.IsWall(c) && !Level.IsDoor(c);
    }

    public IEnumerable<Champion> Living => Party.Where(c => c.Alive);

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, IncludeFields = false };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static WorldState FromJson(string json) => JsonSerializer.Deserialize<WorldState>(json, Options) ?? throw new InvalidDataException("Not a saved game.");
}

