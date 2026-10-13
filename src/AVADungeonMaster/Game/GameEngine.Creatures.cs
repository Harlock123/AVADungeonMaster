using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

public sealed partial class GameEngine
{
    public const int SenseRange = 9;

    public Creature? CreatureAt(int x, int y) => World.State.Creatures.FirstOrDefault(c => c.X == x && c.Y == y && c.Health > 0);

    /// <summary>Every creature on the party's level takes its turn when its clock says so.</summary>
    private void LiveCreatures(double dt)
    {
        var w = World;
        foreach (var cr in w.State.Creatures.Where(c => c.Health > 0).ToList())
        {
            var kind = cr.Info;
            if (cr.Flash > 0) cr.Flash -= dt;
            if (cr.Bound > 0)
            {
                cr.Bound -= dt;
                continue;
            }
            cr.MoveTimer -= dt;
            cr.AttackTimer -= dt;
            int dist = Math.Abs(cr.X - w.X) + Math.Abs(cr.Y - w.Y);
            if (dist == 1)
            {
                if (cr.AttackTimer <= 0)
                {
                    cr.AttackTimer = kind.AttackTime * (0.85 + Rng.Next(30) / 100.0);
                    Strike(cr);
                }
                continue;
            }
            // Spellcasters throw fire down a straight, clear line.
            if (kind.Caster && cr.AttackTimer <= 0 && dist <= 5 && ClearLine(cr.X, cr.Y, w.X, w.Y))
            {
                cr.AttackTimer = kind.AttackTime * 1.6;
                var dir = cr.X == w.X ? (cr.Y < w.Y ? Dir.South : Dir.North) : (cr.X < w.X ? Dir.East : Dir.West);
                Launch(new Projectile { X = cr.X, Y = cr.Y, Dir = dir, Damage = kind.Damage, Kind = SpellKind.Fireball, FromParty = false });
                Play(Sfx.Fireball);
                continue;
            }
            if (cr.MoveTimer > 0) continue;
            cr.MoveTimer = kind.MoveTime * (0.85 + Rng.Next(30) / 100.0);
            (int X, int Y)? step = null;
            if (dist <= SenseRange && !(kind.Erratic && Rng.Next(100) < 40)) step = StepToward(cr, w.X, w.Y);
            step ??= Wander(cr);
            if (step is { } s)
            {
                cr.X = s.X;
                cr.Y = s.Y;
            }
        }
        w.State.Creatures.RemoveAll(c => c.Health <= 0);
    }

    private bool Free(int x, int y) =>
        World.Walkable(x, y) && Here.At(x, y) is not (Cell.Pit or Cell.StairsUp or Cell.StairsDown or Cell.Teleporter)
        && CreatureAt(x, y) is null && (x, y) != (World.X, World.Y);

    /// <summary>The first step along the shortest way to the party (through open squares only).</summary>
    private (int X, int Y)? StepToward(Creature cr, int tx, int ty)
    {
        var prev = new Dictionary<(int, int), (int, int)> { [(cr.X, cr.Y)] = (cr.X, cr.Y) };
        var q = new Queue<(int, int)>();
        q.Enqueue((cr.X, cr.Y));
        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            foreach (Dir d in Enum.GetValues<Dir>())
            {
                var (dx, dy) = d.Step();
                var n = (x + dx, y + dy);
                if (prev.ContainsKey(n)) continue;
                if (n == (tx, ty))
                {
                    var at = (x, y);
                    if (at == (cr.X, cr.Y)) return null;
                    while (prev[at] != (cr.X, cr.Y)) at = prev[at];
                    return at;
                }
                if (!Free(n.Item1, n.Item2) || Math.Abs(n.Item1 - cr.X) + Math.Abs(n.Item2 - cr.Y) > SenseRange + 4) continue;
                prev[n] = (x, y);
                q.Enqueue(n);
            }
        }
        return null;
    }

    private (int X, int Y)? Wander(Creature cr)
    {
        var options = Enum.GetValues<Dir>().Select(d => (cr.X + d.Step().X, cr.Y + d.Step().Y)).Where(p => Free(p.Item1, p.Item2)).ToList();
        if (options.Count == 0 || Rng.Next(100) < 40) return null;
        return Rng.Pick(options);
    }

    private bool ClearLine(int x0, int y0, int x1, int y1)
    {
        if (x0 != x1 && y0 != y1) return false;
        int dx = Math.Sign(x1 - x0), dy = Math.Sign(y1 - y0);
        for (int x = x0 + dx, y = y0 + dy; (x, y) != (x1, y1); x += dx, y += dy)
            if (!World.Walkable(x, y)) return false;
        return true;
    }

    /// <summary>
    /// A creature next to the party strikes whoever is on its side of the formation: the front pair
    /// from ahead, the back pair from behind, the left or right file from the sides.
    /// </summary>
    private void Strike(Creature cr)
    {
        var w = World;
        var f = w.Facing.Step();
        var r = w.Facing.Right().Step();
        int ox = cr.X - w.X, oy = cr.Y - w.Y;
        int ahead = ox * f.X + oy * f.Y, right = ox * r.X + oy * r.Y;
        int[] slots = ahead > 0 ? [0, 1] : ahead < 0 ? [2, 3] : right > 0 ? [1, 3] : [0, 2];
        var targets = w.Party.Where(c => c.Alive && slots.Contains(c.Slot)).ToList();
        if (targets.Count == 0) targets = w.Party.Where(c => c.Alive).ToList();
        if (targets.Count == 0) return;
        var victim = Rng.Pick(targets);
        var kind = cr.Info;
        int chance = Math.Clamp(88 - victim.Dexterity / 4 - victim.ArmourValue, 25, 92);
        if (Rng.Next(100) >= chance)
        {
            Say($"The {kind.Name} misses {First(victim)}.");
            Learn(victim, Skill.Ninja, 2);
            return;
        }
        int dmg = Math.Max(1, (int)(kind.Damage * 1.5 * (0.7 + Rng.Next(61) / 100.0)) - victim.ArmourValue / 3);
        Say($"The {kind.Name} hits {First(victim)} for {dmg}.", 0xFFFF8060);
        HurtChampion(victim, dmg);
        if (kind.Poison && victim.Alive && Rng.Next(100) < 35)
        {
            victim.Poison = 20;
            Say($"{First(victim)} is poisoned!", 0xFF80FF60);
        }
        if (kind.Spirit && victim.Mana > 0) victim.Mana = Math.Max(0, victim.Mana - 3);
    }

    /// <summary>Hurts a creature; when it dies the champion who did it learns from it.</summary>
    public void HurtCreature(Creature cr, int damage, Champion? by, Skill skill)
    {
        if (cr.Health <= 0) return;
        if (cr.Bound > 0) damage *= 2;
        cr.Health -= damage;
        cr.Flash = 0.15;
        if (by is not null) Learn(by, skill, Math.Max(1, damage / 2));
        if (cr.Health > 0)
        {
            Play(Sfx.MonsterHurt);
            Say($"{(by is null ? "The" : First(by) + " hits the")} {cr.Info.Name} for {damage}.");
            return;
        }
        Play(Sfx.MonsterDie);
        World.Kills++;
        Say($"The {cr.Info.Name} is destroyed!", 0xFFFFD860);
        if (by is not null) Learn(by, skill, cr.Info.Xp);
        if (cr.Kind == "vorthane") Victory();
    }
}
