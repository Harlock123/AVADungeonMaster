using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

/// <summary>Something flying down a corridor: a thrown dagger, an arrow, a fireball.</summary>
public sealed class Projectile
{
    public int X, Y;
    /// <summary>How far it is toward the next square (0–1), for drawing it in between.</summary>
    public double Progress;
    public Dir Dir;
    public int Damage;
    public SpellKind? Kind;
    public Thing? Thing;
    public bool FromParty;
    public int Shooter = -1;
    public Skill Skill;
    public int Range = 8;
    public bool Done;
    public const double Speed = 5;
}

/// <summary>A burst where something hit, for the view to draw.</summary>
public sealed class Blast
{
    public int X, Y;
    public double Age;
    public uint Colour;
}

/// <summary>Poison air hanging in a square.</summary>
public sealed class Cloud
{
    public int X, Y;
    public int Power;
    public double Time;
    public double Tick;
}

public sealed partial class GameEngine
{
    public List<Projectile> Projectiles { get; } = [];
    public List<Blast> Blasts { get; } = [];
    public List<Cloud> Clouds { get; } = [];
    /// <summary>Who is speaking the runes.</summary>
    public int Caster { get; private set; }
    public List<string> Runes_ { get; } = [];
    public IReadOnlyList<string> Spoken => Runes_;

    private void Launch(Projectile p) => Projectiles.Add(p);

    private void LiveMagic(double dt)
    {
        foreach (var b in Blasts) b.Age += dt;
        Blasts.RemoveAll(b => b.Age > 0.5);
        foreach (var p in Projectiles.ToList())
        {
            p.Progress += Projectile.Speed * dt;
            while (p.Progress >= 1 && !p.Done)
            {
                p.Progress -= 1;
                var (dx, dy) = p.Dir.Step();
                int nx = p.X + dx, ny = p.Y + dy;
                if (!World.Walkable(nx, ny) || p.Range-- <= 0)
                {
                    Impact(p, p.X, p.Y);
                    break;
                }
                p.X = nx;
                p.Y = ny;
                if (p.FromParty && CreatureAt(nx, ny) is { } cr) { HitCreature(p, cr); Impact(p, nx, ny); }
                else if (!p.FromParty && (nx, ny) == (World.X, World.Y)) { HitParty(p); Impact(p, nx, ny); }
            }
        }
        Projectiles.RemoveAll(p => p.Done);

        foreach (var c in Clouds)
        {
            c.Time -= dt;
            c.Tick -= dt;
            if (c.Tick > 0) continue;
            c.Tick = 1;
            if (CreatureAt(c.X, c.Y) is { } cr && !cr.Info.Spirit) HurtCreature(cr, 2 + 2 * c.Power, null, Skill.Wizard);
            if ((c.X, c.Y) == (World.X, World.Y))
                foreach (var ch in World.Party.Where(ch => ch.Alive).ToList()) HurtChampion(ch, 1 + c.Power / 2, quiet: true);
        }
        Clouds.RemoveAll(c => c.Time <= 0);
    }

    private void HitCreature(Projectile p, Creature cr)
    {
        var by = p.Shooter >= 0 && p.Shooter < World.Party.Count ? World.Party[p.Shooter] : null;
        int dmg = p.Damage;
        bool spirit = cr.Info.Spirit;
        if (p.Thing is { } t)
        {
            if (spirit && !t.Item.Hallowed) { Say($"It passes straight through the {cr.Info.Name}."); return; }
            dmg = Math.Max(1, dmg - cr.Info.Armour);
        }
        else if (p.Kind == SpellKind.Unmaking) dmg = spirit ? dmg : dmg / 2;
        else if (spirit && p.Kind != SpellKind.Fireball && p.Kind != SpellKind.Lightning) return;
        HurtCreature(cr, Math.Max(1, dmg), by, p.Skill);
    }

    private void HitParty(Projectile p)
    {
        // Fire bursts over the front of the party.
        var front = World.Party.Where(c => c.Alive && c.Front).ToList();
        if (front.Count == 0) front = World.Party.Where(c => c.Alive).ToList();
        foreach (var c in front) HurtChampion(c, Math.Max(1, (int)(p.Damage * (0.7 + Rng.Next(40) / 100.0)) - c.ArmourValue / 4));
        Say("Fire bursts over the party!", 0xFFFF8040);
    }

    private void Impact(Projectile p, int x, int y)
    {
        p.Done = true;
        if (p.Thing is { } t)
        {
            World.State.Put(x, y, t);
            return;
        }
        Blasts.Add(new Blast
        {
            X = x, Y = y,
            Colour = p.Kind switch { SpellKind.Lightning => 0xFFA0E0FF, SpellKind.Unmaking => 0xFFC080FF, _ => 0xFFFF8030 },
        });
        Play(Sfx.Explode);
    }

    // ───────────────────────────── the runes ─────────────────────────────

    public void NextCaster()
    {
        if (World.Party.Count == 0) return;
        for (int i = 1; i <= World.Party.Count; i++)
        {
            int n = (Caster + i) % World.Party.Count;
            if (World.Party[n].Alive) { Caster = n; break; }
        }
        Runes_.Clear();
        Play(Sfx.Menu);
    }

    /// <summary>Speaks the n-th rune of the row that comes next.</summary>
    public void Rune(int n)
    {
        if (World.Party.Count == 0 || Runes_.Count >= 4) return;
        var row = Runes.Row(Runes_.Count);
        if (n >= row.Length) return;
        Runes_.Add(row[n]);
        Play(Sfx.Menu);
    }

    public void UnRune()
    {
        if (Runes_.Count > 0) Runes_.RemoveAt(Runes_.Count - 1);
    }

    /// <summary>Casts the spoken runes: mana is spent whether or not the spell takes.</summary>
    public void Cast()
    {
        if (World.Party.Count == 0 || Runes_.Count == 0) return;
        Caster = Math.Min(Caster, World.Party.Count - 1);
        var c = World.Party[Caster];
        if (!c.Alive) { Say("The dead cast no spells."); Runes_.Clear(); return; }
        if (c.Cooldown > 0) return;
        int cost = Runes.Cost(Runes_);
        if (c.Mana < cost)
        {
            Say($"{First(c)} hasn't the mana for that ({cost} needed).");
            Play(Sfx.Fizzle);
            return;
        }
        c.Mana -= cost;
        c.Cooldown = 1.0;
        int power = Array.IndexOf(Runes.Power, Runes_[0]) + 1;
        var spell = Spell.Match(Runes_);
        string spoken = string.Join(" ", Runes_);
        Runes_.Clear();
        if (spell is null || power <= 0)
        {
            Say($"{First(c)} speaks {spoken}... and nothing happens.", 0xFFA0A0C0);
            Play(Sfx.Fizzle);
            Learn(c, Skill.Wizard, 1);
            return;
        }
        bool staff = c.Hands.Any(t => t.Id is "emberstaff" or "starheart");
        int chance = Math.Clamp(50 + 12 * c.Level(spell.Skill) - 10 * power + c.Wisdom / 3 + (staff ? 15 : 0), 10, 98);
        if (Rng.Next(100) >= chance)
        {
            Say($"{First(c)}'s {spell.Name} fizzles.", 0xFFA0A0C0);
            Play(Sfx.Fizzle);
            Learn(c, spell.Skill, 2);
            return;
        }
        if (!World.Spellbook.Contains(spell.Name)) World.Spellbook.Add(spell.Name);
        Learn(c, spell.Skill, 6 * power);
        double boost = staff ? 1.25 : 1;
        var (fx, fy) = Ahead;
        switch (spell.Kind)
        {
            case SpellKind.Light:
                World.MagicLight = Math.Min(400, World.MagicLight + 40 * power);
                World.MagicLightPower = Math.Max(World.MagicLightPower, 0.5 + power * 0.3);
                Say("A soft light gathers around the party.", 0xFFFFF0A0);
                Play(Sfx.Spell);
                break;
            case SpellKind.Fireball or SpellKind.Lightning or SpellKind.Unmaking:
            {
                int dmg = (int)(spell.Kind switch { SpellKind.Fireball => 6 + 6 * power, SpellKind.Lightning => 5 + 7 * power, _ => 8 + 8 * power } * boost);
                Launch(new Projectile { X = World.X, Y = World.Y, Dir = World.Facing, Damage = dmg, Kind = spell.Kind, FromParty = true, Shooter = Caster, Skill = Skill.Wizard });
                Play(spell.Kind == SpellKind.Lightning ? Sfx.Lightning : Sfx.Fireball);
                break;
            }
            case SpellKind.PoisonCloud:
                if (!World.Walkable(fx, fy)) { Say("There's no room for the cloud."); break; }
                Clouds.Add(new Cloud { X = fx, Y = fy, Power = power, Time = 5 + 2 * power });
                Play(Sfx.Spell);
                break;
            case SpellKind.Heal:
            {
                var who = World.Party.Where(p => p.Alive).OrderBy(p => (double)p.Health / p.MaxHealth).First();
                int amount = (int)((10 + 10 * power) * boost);
                who.Health = Math.Min(who.MaxHealth, who.Health + amount);
                who.Poison = 0;
                Say($"{First(who)}'s wounds close (+{amount}).", 0xFF80FF80);
                Play(Sfx.Heal);
                break;
            }
            case SpellKind.Ward:
                World.Ward = Math.Max(World.Ward, 15 + 10 * power);
                Say("A shimmering ward settles over the party.", 0xFF80C0FF);
                Play(Sfx.Spell);
                break;
            case SpellKind.Vigour:
                foreach (var p in World.Party.Where(p => p.Alive)) p.Stamina = Math.Min(p.MaxStamina, p.Stamina + 10 + 10 * power);
                Say("Strength floods back into tired arms.", 0xFF80FF80);
                Play(Sfx.Heal);
                break;
            case SpellKind.Open:
                if (Here.At(fx, fy) == Cell.RuneDoor && !World.State.Open.Contains(LevelState.Key(fx, fy)))
                {
                    World.State.Open.Add(LevelState.Key(fx, fy));
                    Say("The runes flare and fade. The door swings open.", 0xFF80C0FF);
                    Play(Sfx.Door);
                }
                else Say("The spell finds nothing to unseal.");
                break;
        }
    }

    /// <summary>Reading a scroll puts its spell in the spellbook.</summary>
    public void Learn(Item scroll)
    {
        if (scroll.Kind != ItemKind.Scroll) return;
        string? name = scroll.Id switch
        {
            "scroll_light" => "Light", "scroll_fire" => "Fireball", "scroll_heal" => "Mend", "scroll_ward" => "Ward", "scroll_storm" => "Storm Spear",
            "scroll_unmake" => "Unmaking", "scroll_open" => "Unseal", "scroll_cloud" => "Foul Cloud", "scroll_vigour" => "Second Wind", _ => null,
        };
        if (name is not null && !World.Spellbook.Contains(name))
        {
            World.Spellbook.Add(name);
            Say($"The scroll reads: {scroll.Text}", 0xFFFFF0C0);
        }
    }
}
