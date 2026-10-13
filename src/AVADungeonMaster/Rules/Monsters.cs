namespace AVADungeonMaster.Rules;

/// <summary>
/// A kind of creature of the deep. Each moves and strikes on its own clock; some throw fire, some
/// poison, and spirits can only be hurt by magic or a hallowed blade. All original.
/// </summary>
public sealed record MonsterKind(string Id, string Name, int Health, int Damage, int Armour, double MoveTime, double AttackTime, int Xp, string Sprite, uint Colour)
{
    public bool Spirit { get; init; }
    public bool Poison { get; init; }
    /// <summary>Throws fireballs from a distance.</summary>
    public bool Caster { get; init; }
    /// <summary>Flits about at random as much as it closes in.</summary>
    public bool Erratic { get; init; }
    public bool Boss { get; init; }
    public double Size { get; init; } = 1;

    public static readonly IReadOnlyList<MonsterKind> All =
    [
        new("mudling", "Mudling", 45, 6, 0, 1.6, 1.5, 12, "slime", 0xFF6A8A3A),
        new("gnawer", "Gnawer", 26, 5, 0, 0.7, 1.1, 8, "rat", 0xFF8A7058) { Size = 0.7 },
        new("bonesentry", "Bone Sentry", 75, 10, 2, 1.2, 1.4, 25, "skeleton", 0xFFE0D8C0),
        new("gloomwing", "Gloomwing", 34, 7, 0, 0.5, 1.0, 15, "bat", 0xFF4A3A5A) { Erratic = true, Size = 0.7 },
        new("crawler", "Crawler", 100, 12, 2, 1.0, 1.3, 40, "spider", 0xFF3A3A3A) { Poison = true },
        new("wisp", "Wisp", 80, 11, 0, 0.9, 1.4, 50, "wisp", 0xFF90D0FF) { Spirit = true },
        new("shellback", "Shellback", 140, 15, 6, 1.4, 1.6, 60, "crab", 0xFFC06030),
        new("emberimp", "Ember Imp", 100, 13, 1, 1.0, 2.0, 70, "imp", 0xFFE04020) { Caster = true },
        new("ashgolem", "Ash Golem", 220, 22, 5, 1.8, 2.0, 110, "golem", 0xFF6A6A6A) { Size = 1.2 },
        new("warden", "Iron Warden", 200, 20, 8, 1.4, 1.6, 150, "knight", 0xFF8A90A0) { Size = 1.15 },
        new("deepwyrm", "The Deepwyrm", 600, 26, 6, 1.6, 1.8, 600, "wyrm", 0xFF3A8A5A) { Caster = true, Boss = true, Size = 1.5 },
        new("vorthane", "Vorthane the Unmade", 1400, 34, 5, 1.2, 1.5, 2000, "regent", 0xFFB060FF) { Caster = true, Spirit = true, Boss = true, Size = 1.4 },
    ];

    private static readonly Dictionary<string, MonsterKind> ById = All.ToDictionary(m => m.Id);
    public static MonsterKind Get(string id) => ById[id];
}
