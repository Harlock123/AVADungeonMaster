namespace AVADungeonMaster.Rules;

/// <summary>
/// The rune language: a spell is one POWER rune (how strong), then an element, then sometimes a form.
/// Anything can be spoken; only the right sequences do anything. All of it is this game's own.
/// </summary>
public static class Runes
{
    public static readonly string[] Power = ["IN", "OS", "UR", "EL", "VAR", "THAN"];
    public static readonly string[] Element = ["FLA", "VOL", "AQU", "TER", "LUM", "MOR"];
    public static readonly string[] Form = ["JAC", "NUB", "SCU", "SUI"];

    /// <summary>The runes that can come next after a sequence of this many.</summary>
    public static string[] Row(int spoken) => spoken switch { 0 => Power, 1 => Element, _ => Form };

    /// <summary>What each rune costs in mana, multiplied by the power.</summary>
    public static int Cost(IReadOnlyList<string> runes) => runes.Count == 0 ? 0 : (Array.IndexOf(Power, runes[0]) + 1) * (1 + runes.Count);
}

public enum SpellKind { Light, Fireball, Lightning, Unmaking, PoisonCloud, Heal, Ward, Vigour, Open }

/// <summary>A spell: the runes after the power rune, and which skill it uses.</summary>
public sealed record Spell(SpellKind Kind, string Name, string Runes, Skill Skill, string Text)
{
    public static readonly IReadOnlyList<Spell> All =
    [
        new(SpellKind.Light, "Light", "LUM", Skill.Wizard, "A glowing light around the party, brighter and longer with more power."),
        new(SpellKind.Fireball, "Fireball", "FLA JAC", Skill.Wizard, "A ball of fire thrown ahead, bursting on whatever it hits."),
        new(SpellKind.Lightning, "Storm Spear", "VOL JAC", Skill.Wizard, "A bolt of lightning thrown ahead."),
        new(SpellKind.Unmaking, "Unmaking", "MOR JAC", Skill.Wizard, "A bolt that tears at spirits; flesh only half feels it."),
        new(SpellKind.PoisonCloud, "Foul Cloud", "MOR NUB", Skill.Wizard, "Poison air in the square ahead, hurting whatever is in it."),
        new(SpellKind.Heal, "Mend", "AQU SUI", Skill.Priest, "Heals the most wounded champion."),
        new(SpellKind.Ward, "Ward", "TER SCU", Skill.Priest, "Turns aside half of every blow for a while."),
        new(SpellKind.Vigour, "Second Wind", "VOL SUI", Skill.Priest, "Puts stamina back into the whole party."),
        new(SpellKind.Open, "Unseal", "TER", Skill.Wizard, "Opens a rune-sealed door ahead."),
    ];

    /// <summary>The spell a sequence of runes makes, if any (the first rune must be a power rune).</summary>
    public static Spell? Match(IReadOnlyList<string> runes)
    {
        if (runes.Count < 2 || Array.IndexOf(global::AVADungeonMaster.Rules.Runes.Power, runes[0]) < 0) return null;
        string rest = string.Join(" ", runes.Skip(1));
        return All.FirstOrDefault(s => s.Runes == rest);
    }
}
