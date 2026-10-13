namespace AVADungeonMaster.Rules;

public enum Skill { Fighter, Ninja, Priest, Wizard }

/// <summary>An actual item: its kind, and what's left of it (a torch's seconds, a waterskin's drinks).</summary>
public sealed class Thing
{
    public string Id { get; set; } = "";
    public double Charge { get; set; }

    public Thing() { }

    public Thing(string id)
    {
        Id = id;
        var item = Items.Get(id);
        Charge = item.Kind switch { ItemKind.Torch => item.Burn, ItemKind.Drink => 3, _ => 0 };
    }

    public Item Item => Items.Get(Id);
    public override string ToString() => Item.Name;
}

/// <summary>
/// One of the party: health, stamina and mana, four skills that grow with use, food and water that
/// run down, two hands and a pack. Saved as it is.
/// </summary>
public sealed class Champion
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public uint Colour { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public double Stamina { get; set; }
    public int MaxStamina { get; set; }
    public double Mana { get; set; }
    public int MaxMana { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Wisdom { get; set; }
    public int Vitality { get; set; }
    /// <summary>Experience in each skill.</summary>
    public int[] Xp { get; set; } = new int[4];
    public double Food { get; set; } = 100;
    public double Water { get; set; } = 100;
    public Thing? Left { get; set; }
    public Thing? Right { get; set; }
    public Thing? Armour { get; set; }
    public List<Thing> Pack { get; set; } = [];
    /// <summary>Seconds before this champion can act again.</summary>
    public double Cooldown { get; set; }
    public double Poison { get; set; }
    /// <summary>Where in the party's 2×2 formation: 0 front-left, 1 front-right, 2 back-left, 3 back-right.</summary>
    public int Slot { get; set; }

    public bool Alive => Health > 0;
    public bool Front => Slot < 2;

    public const int PackSize = 8;
    public static readonly int[] LevelXp = [0, 100, 300, 700, 1500, 3000, 6000, 12000, 24000];
    public static readonly string[] Ranks = ["", "Neophyte", "Novice", "Apprentice", "Journeyman", "Craftsman", "Artisan", "Adept", "Expert", "Master"];

    public int Level(Skill s)
    {
        int xp = Xp[(int)s], level = 0;
        while (level < LevelXp.Length && xp >= LevelXp[level]) level++;
        return level;
    }

    public string Rank(Skill s) => Ranks[Math.Clamp(Level(s), 0, 9)];

    public IEnumerable<Thing> Hands => new[] { Right, Left }.Where(h => h is not null)!;

    public int ArmourValue =>
        (Armour?.Item.Armour ?? 0) + Hands.Where(h => h.Item.Kind == ItemKind.Shield).Sum(h => h.Item.Armour) + Dexterity / 10;

    /// <summary>What's in hand to fight with: the right hand's weapon, else the left's, else fists.</summary>
    public Thing? Weapon => Hands.FirstOrDefault(h => h.Item.IsWeapon);

    public IEnumerable<WeaponAction> Actions => Weapon?.Item.Actions ?? [Items.BareHands];

    /// <summary>How much light the torches in hand give (a guttering torch gives less).</summary>
    public double LightGiven => Hands.Where(h => h.Item.Kind == ItemKind.Torch && h.Charge > 0).Sum(h => Math.Clamp(h.Charge / 120, 0.3, 1.0));

    /// <summary>Everything carried, hands and armour and pack.</summary>
    public IEnumerable<Thing> Everything => new[] { Left, Right, Armour }.Where(t => t is not null).Concat(Pack)!;

    /// <summary>Learns from doing: returns the new level if the skill went up.</summary>
    public int? Gain(Skill s, int xp)
    {
        int before = Level(s);
        Xp[(int)s] += xp;
        int after = Level(s);
        if (after <= before) return null;
        // Each level makes the champion tougher, steadier, and (for casters) deeper in mana.
        MaxHealth += s == Skill.Fighter ? 6 : 3;
        MaxStamina += s is Skill.Fighter or Skill.Ninja ? 5 : 3;
        if (s is Skill.Priest or Skill.Wizard) MaxMana += 4;
        switch (s)
        {
            case Skill.Fighter: Strength += 2; break;
            case Skill.Ninja: Dexterity += 2; break;
            default: Wisdom += 2; break;
        }
        Vitality++;
        return after;
    }

    public void Hurt(int damage)
    {
        Health = Math.Max(0, Health - damage);
    }
}
