using AVADungeonMaster.Rules;

namespace AVADungeonMaster.World;

/// <summary>The twelve champions bound in the Hall of Mirrors. All original.</summary>
public static class Roster
{
    private static Champion C(string name, string title, uint colour, int hp, int st, int mana, int str, int dex, int wis, int vit,
        (int F, int N, int P, int W) xp, params string[] kit)
    {
        var c = new Champion
        {
            Name = name, Title = title, Colour = colour, Health = hp, MaxHealth = hp, Stamina = st, MaxStamina = st, Mana = mana, MaxMana = mana,
            Strength = str, Dexterity = dex, Wisdom = wis, Vitality = vit, Xp = [xp.F, xp.N, xp.P, xp.W],
        };
        foreach (var k in kit)
        {
            var t = new Thing(k);
            var item = t.Item;
            if (item.Kind == ItemKind.Armour && c.Armour is null) c.Armour = t;
            else if ((item.IsWeapon || item.Kind is ItemKind.Shield or ItemKind.Torch) && c.Right is null && item.Kind != ItemKind.Shield) c.Right = t;
            else if ((item.Kind is ItemKind.Shield or ItemKind.Torch || item.IsWeapon) && c.Left is null) c.Left = t;
            else c.Pack.Add(t);
        }
        return c;
    }

    /// <summary>A fresh copy of every champion, in mirror order.</summary>
    public static List<Champion> Make() =>
    [
        C("Brynja Ironhand", "a shieldmaiden of the north", 0xFFD05040, 62, 55, 0, 52, 38, 22, 48, (300, 0, 0, 0), "sword", "buckler", "jerkin"),
        C("Taric of the Fen", "a fen-runner and archer", 0xFF50A050, 44, 62, 0, 38, 54, 30, 40, (0, 300, 0, 0), "dagger", "rock", "rock", "jerkin"),
        C("Old Wenna", "a hedge-priestess", 0xFFE0C050, 40, 40, 24, 30, 34, 50, 42, (0, 0, 300, 0), "staff", "apple"),
        C("Ysmar the Grey", "a wizard of the high towers", 0xFF8090D0, 34, 36, 30, 26, 36, 56, 34, (0, 0, 0, 300), "staff", "scroll_light"),
        C("Kell Ashborn", "a sellsword", 0xFFE08030, 54, 58, 0, 46, 48, 26, 44, (100, 100, 0, 0), "club", "dagger"),
        C("Sister Orla", "a battle-nun", 0xFFC8C8D8, 50, 48, 16, 42, 34, 46, 46, (100, 0, 100, 0), "mace", "jerkin"),
        C("Fenwick Quill", "a scholar with sharp knives", 0xFF60B0B0, 38, 46, 22, 30, 48, 50, 36, (0, 100, 0, 100), "dagger", "dagger"),
        C("Haldor Stoneback", "a mountain-hold warder", 0xFF908070, 64, 50, 12, 50, 30, 40, 52, (100, 0, 100, 0), "club", "buckler"),
        C("Mira Swiftwater", "a river-witch", 0xFF4080E0, 40, 54, 22, 30, 52, 48, 38, (0, 100, 0, 100), "sling", "rock", "rock", "rock"),
        C("Gorm the Patient", "a hermit of two arts", 0xFF7A6AB0, 42, 40, 30, 32, 32, 54, 44, (0, 0, 100, 100), "staff", "cheese"),
        C("Jessamy Vane", "a duellist-mage", 0xFFD060B0, 46, 48, 20, 40, 44, 48, 40, (100, 0, 0, 100), "sword"),
        C("Rook", "nobody's fool", 0xFF505060, 42, 64, 0, 36, 58, 30, 38, (0, 300, 0, 0), "dagger", "dagger", "rock"),
    ];
}
