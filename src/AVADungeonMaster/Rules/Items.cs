namespace AVADungeonMaster.Rules;

public enum ItemKind { Weapon, Shield, Armour, Food, Drink, Potion, Torch, Key, Scroll, Missile, Treasure, Bones }

/// <summary>One thing a champion can do with an item in hand: swing it, throw it, shoot with it.</summary>
public sealed record WeaponAction(string Name, Skill Skill, int Damage, double Cooldown, int Stamina, bool Ranged = false);

/// <summary>
/// A kind of thing in the dungeon. Weapons list what can be done with them; food and drink how much
/// they fill; a torch how long it burns; a key which locks it opens; a scroll what it says.
/// </summary>
public sealed record Item(string Id, string Name, ItemKind Kind, char Icon, uint Colour)
{
    public WeaponAction[] Actions { get; init; } = [];
    public int Armour { get; init; }
    public int Food { get; init; }
    public int Water { get; init; }
    public int Heal { get; init; }
    public int StaminaGain { get; init; }
    public int ManaGain { get; init; }
    /// <summary>Seconds a torch burns.</summary>
    public double Burn { get; init; }
    public string Text { get; init; } = "";
    /// <summary>Only the right weapon or magic hurts a spirit; this one does.</summary>
    public bool Hallowed { get; init; }
    /// <summary>What a bow or sling needs in the other hand to shoot.</summary>
    public string? Ammo { get; init; }
    public int Weight { get; init; } = 1;

    public bool IsWeapon => Actions.Length > 0;
}

public static class Items
{
    private static readonly WeaponAction Punch = new("PUNCH", Skill.Fighter, 3, 0.8, 1);
    public static WeaponAction BareHands => Punch;

    private static Item W(string id, string name, char icon, uint colour, params WeaponAction[] actions) =>
        new(id, name, ItemKind.Weapon, icon, colour) { Actions = actions };

    public static readonly IReadOnlyList<Item> All =
    [
        W("dagger", "Dagger", '/', 0xFFC8C8D0, new WeaponAction("STAB", Skill.Fighter, 6, 0.7, 1), new WeaponAction("THROW", Skill.Ninja, 8, 1.0, 1, Ranged: true)),
        W("club", "Club", '|', 0xFF8A6A3A, new WeaponAction("BASH", Skill.Fighter, 7, 1.0, 2)),
        W("sword", "Short Sword", '/', 0xFFD8D8E0, new WeaponAction("SWING", Skill.Fighter, 11, 1.0, 2), new WeaponAction("THRUST", Skill.Fighter, 14, 1.4, 3)),
        W("falchion", "Falchion", '/', 0xFFE8E0C0, new WeaponAction("SLASH", Skill.Fighter, 17, 1.1, 3), new WeaponAction("CLEAVE", Skill.Fighter, 23, 1.7, 5)),
        W("axe", "Battle Axe", 'P', 0xFFB0B0B8, new WeaponAction("CHOP", Skill.Fighter, 20, 1.5, 4), new WeaponAction("HACK", Skill.Fighter, 26, 2.0, 6)),
        W("mace", "Iron Mace", 'T', 0xFF9898A0, new WeaponAction("BASH", Skill.Fighter, 15, 1.3, 3), new WeaponAction("SMITE", Skill.Priest, 19, 1.7, 4)),
        W("staff", "Oak Staff", '|', 0xFFA07840, new WeaponAction("SWING", Skill.Fighter, 6, 1.0, 2)),
        W("emberstaff", "Ember Staff", '|', 0xFFE07030, new WeaponAction("SWING", Skill.Fighter, 10, 1.0, 2)) with { Text = "A wizard's staff: spells cast with it in hand are stronger." },
        W("bow", "Hunting Bow", ')', 0xFFA07840, new WeaponAction("SHOOT", Skill.Ninja, 14, 1.0, 1, Ranged: true)) with { Ammo = "arrow" },
        W("sling", "Sling", '~', 0xFF8A6A4A, new WeaponAction("SLING", Skill.Ninja, 9, 0.9, 1, Ranged: true)) with { Ammo = "rock" },
        W("moonblade", "Moonblade", '/', 0xFFC0E0FF, new WeaponAction("SLASH", Skill.Fighter, 24, 1.0, 3), new WeaponAction("SUNDER", Skill.Fighter, 32, 1.6, 5)) with { Hallowed = true, Text = "Its edge cuts what is not there." },
        W("starheart", "The Starheart Staff", '*', 0xFFFFE070, new WeaponAction("SWING", Skill.Fighter, 18, 1.0, 2), new WeaponAction("BIND", Skill.Priest, 10, 2.5, 10)) with { Hallowed = true, Text = "It hums. Held against the Unmade, it can bind him." },

        new("arrow", "Arrow", ItemKind.Missile, '-', 0xFFC0A060),
        new("rock", "Rock", ItemKind.Missile, 'o', 0xFF8A8A8A),

        new("buckler", "Buckler", ItemKind.Shield, 'O', 0xFF9A7A4A) { Armour = 3 },
        new("towershield", "Tower Shield", ItemKind.Shield, 'O', 0xFFB0B0B8) { Armour = 6 },
        new("jerkin", "Leather Jerkin", ItemKind.Armour, 'A', 0xFF8A5A30) { Armour = 3 },
        new("mail", "Mail Shirt", ItemKind.Armour, 'A', 0xFFA0A0A8) { Armour = 6 },
        new("plate", "Plate Harness", ItemKind.Armour, 'A', 0xFFC8C8D0) { Armour = 10, Weight = 3 },
        new("starweave", "Starweave Robe", ItemKind.Armour, 'A', 0xFF6070D0) { Armour = 7, Text = "Light as cloth, hard as mail." },

        new("bread", "Loaf of Bread", ItemKind.Food, 'b', 0xFFD0A060) { Food = 35 },
        new("cheese", "Cheese", ItemKind.Food, 'c', 0xFFE8D060) { Food = 25 },
        new("apple", "Apple", ItemKind.Food, 'a', 0xFFD03030) { Food = 15 },
        new("meat", "Dried Meat", ItemKind.Food, 'm', 0xFF8A3A2A) { Food = 45 },
        new("waterskin", "Waterskin", ItemKind.Drink, 'w', 0xFF7A5A3A) { Water = 30, Text = "Three good drinks. Fill it at a fountain." },

        new("healing", "Healing Draught", ItemKind.Potion, '!', 0xFFE04040) { Heal = 45 },
        new("vigour", "Vigour Tonic", ItemKind.Potion, '!', 0xFF40C040) { StaminaGain = 50 },
        new("mana", "Mana Flask", ItemKind.Potion, '!', 0xFF4060E0) { ManaGain = 30 },

        new("torch", "Torch", ItemKind.Torch, 'i', 0xFFE0A040) { Burn = 420 },

        new("ironkey", "Iron Key", ItemKind.Key, 'k', 0xFF8A8A90),
        new("brasskey", "Brass Key", ItemKind.Key, 'k', 0xFFD0A040),
        new("bonekey", "Bone Key", ItemKind.Key, 'k', 0xFFE8E0C8),
        new("emberkey", "Ember Key", ItemKind.Key, 'k', 0xFFE06020),
        new("starkey", "Star Key", ItemKind.Key, 'k', 0xFF80C0FF),

        new("scroll_light", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"For light in the deep: speak the POWER, then LUM.\"" },
        new("scroll_fire", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"To throw fire: POWER, then FLA, then JAC.\"" },
        new("scroll_heal", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"To close a wound: POWER, then AQU, then SUI.\"" },
        new("scroll_ward", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"To turn blows aside: POWER, then TER, then SCU.\"" },
        new("scroll_storm", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"The storm's spear: POWER, then VOL, then JAC.\"" },
        new("scroll_unmake", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"Against what is not flesh: POWER, then MOR, then JAC.\"" },
        new("scroll_open", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"A rune door yields to POWER, then TER alone.\"" },
        new("scroll_cloud", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"Foul air for foul things: POWER, then MOR, then NUB.\"" },
        new("scroll_vigour", "Scroll", ItemKind.Scroll, '=', 0xFFE8E0C0) { Text = "\"To put strength back in tired arms: POWER, then VOL, then SUI.\"" },

        new("gem", "Blue Gem", ItemKind.Treasure, '*', 0xFF4080F0),
        new("coin", "Gold Coin", ItemKind.Treasure, '$', 0xFFE8C040),
        new("bones", "Bones", ItemKind.Bones, '%', 0xFFE8E0C8) { Text = "All that is left of a champion. An altar might bring them back." },
    ];

    private static readonly Dictionary<string, Item> ById = All.ToDictionary(i => i.Id);
    public static Item Get(string id) => ById[id];
    public static bool Exists(string id) => ById.ContainsKey(id);
}
