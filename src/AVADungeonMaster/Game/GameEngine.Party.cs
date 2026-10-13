using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

public sealed partial class GameEngine
{
    public const double MoveDelay = 0.18;
    private double _moveCool;
    private double _starveTick;

    /// <summary>The party's place in the world.</summary>
    public (int X, int Y) Ahead => (World.X + World.Facing.Step().X, World.Y + World.Facing.Step().Y);

    // ───────────────────────────── the living world ─────────────────────────────

    /// <summary>One step of the dungeon's own time.</summary>
    private void Live(double dt)
    {
        var w = World;
        w.Time += dt;
        if (_moveCool > 0) _moveCool -= dt;
        if (w.MagicLight > 0) w.MagicLight = Math.Max(0, w.MagicLight - dt);
        if (w.Ward > 0) w.Ward = Math.Max(0, w.Ward - dt);
        bool starving = false;
        foreach (var c in w.Party.Where(c => c.Alive))
        {
            foreach (var t in c.Hands.Where(t => t.Item.Kind == ItemKind.Torch && t.Charge > 0).ToList())
            {
                t.Charge -= dt;
                if (t.Charge <= 0)
                {
                    if (c.Left == t) c.Left = null; else c.Right = null;
                    Say($"{c.Name.Split(' ')[0]}'s torch burns out.", 0xFFE0A040);
                }
            }
            c.Food = Math.Max(0, c.Food - dt * 100 / 900);
            c.Water = Math.Max(0, c.Water - dt * 100 / 600);
            bool hungry = c.Food <= 0 || c.Water <= 0;
            starving |= hungry;
            if (c.Cooldown > 0) c.Cooldown = Math.Max(0, c.Cooldown - dt);
            c.Stamina = Math.Min(c.MaxStamina, c.Stamina + dt * (2 + c.Vitality / 20.0) * (hungry ? 0.4 : 1));
            c.Mana = Math.Min(c.MaxMana, c.Mana + dt * (0.15 + c.Wisdom / 160.0));
            if (!hungry && c.Food > 30 && c.Water > 30 && c.Health < c.MaxHealth && Rng.Next(1000) < (int)(dt * 1000 * c.Vitality / 300.0)) c.Health++;
            if (c.Poison > 0)
            {
                c.Poison -= dt;
                if (Rng.Next(1000) < (int)(dt * 600)) HurtChampion(c, 1, quiet: true);
            }
        }
        if (starving)
        {
            _starveTick += dt;
            if (_starveTick > 4)
            {
                _starveTick = 0;
                foreach (var c in w.Party.Where(c => c.Alive && (c.Food <= 0 || c.Water <= 0)).ToList()) HurtChampion(c, 1, quiet: true);
                Say("Hunger and thirst gnaw at the party.", 0xFFE08060);
            }
        }
        LiveCreatures(dt);
        LiveMagic(dt);
        if (Mode == Mode.Play && w.Party.Count > 0 && !w.Party.Any(c => c.Alive)) PartyDead();
    }

    /// <summary>How much light the party has: torches in hand, magic, and what the level gives.</summary>
    public double Light
    {
        get
        {
            var w = World;
            double torches = w.Party.Where(c => c.Alive).Sum(c => c.LightGiven);
            double magic = w.MagicLight > 0 ? w.MagicLightPower * Math.Min(1, w.MagicLight / 30) : 0;
            double ambient = w.Depth == 0 ? 2.5 : 0.25;
            return torches + magic + ambient;
        }
    }

    /// <summary>How many squares ahead the party can see, from its light.</summary>
    public int ViewDepth => Math.Clamp(1 + (int)(Light * 2), 1, 5);

    // ───────────────────────────── moving ─────────────────────────────

    private void Turn(Dir d)
    {
        World.Facing = d;
        Play(Sfx.Step);
    }

    public bool Move(Dir d)
    {
        if (_moveCool > 0) return false;
        var (dx, dy) = d.Step();
        int nx = World.X + dx, ny = World.Y + dy;
        if (!World.Walkable(nx, ny) || CreatureAt(nx, ny) is not null)
        {
            Play(Sfx.Bump);
            Bumped = 0.12;
            if (CreatureAt(nx, ny) is { } cr) Say($"A {cr.Info.Name} is in the way.");
            return false;
        }
        if (Here.At(nx, ny) == Cell.StairsDown && World.Depth == 0 && World.Party.Count == 0)
        {
            Say("Choose at least one champion from the mirrors before you go down.", 0xFFFFD860);
            Play(Sfx.Bump);
            return false;
        }
        World.X = nx;
        World.Y = ny;
        _moveCool = MoveDelay;
        Play(Sfx.Step);
        Arrived();
        return true;
    }

    /// <summary>What happens on stepping onto a square: stairs, pits, teleporters, plates.</summary>
    private void Arrived()
    {
        int x = World.X, y = World.Y;
        switch (Here.At(x, y))
        {
            case Cell.StairsDown: GoTo(World.Depth + 1, down: true); break;
            case Cell.StairsUp: GoTo(World.Depth - 1, down: false); break;
            case Cell.Pit: Fall(); break;
            case Cell.Teleporter:
                if (Here.Teleports.TryGetValue((x, y), out var to))
                {
                    World.X = to.X;
                    World.Y = to.Y;
                    Play(Sfx.Teleport);
                    Say("The world lurches - you are somewhere else.", 0xFF80C0FF);
                    Arrived();
                }
                break;
            case Cell.Plate:
                Play(Sfx.Click);
                Fire(x, y);
                break;
        }
        if (World.State.ThingsAt(World.X, World.Y).Count > 0)
            Say($"Lying here: {string.Join(", ", World.State.ThingsAt(World.X, World.Y).Select(t => t.Item.Name))}. (G to take)", 0xFFB0C0FF);
    }

    /// <summary>Takes the stairs to another level, arriving on the stairs that lead back.</summary>
    public void GoTo(int depth, bool down)
    {
        var target = Levels.Get(depth);
        var (x, y) = target.Find(down ? '<' : '>');
        if (x < 0) (x, y) = target.Start ?? (1, 1);
        Arrive(depth, x, y);
        Play(Sfx.Stairs);
    }

    /// <summary>Puts the party on a level, facing the way out of the square.</summary>
    public void Arrive(int depth, int x, int y, Dir? facing = null)
    {
        bool changed = World.Depth != depth || !World.State.Visited;
        World.Depth = depth;
        World.X = x;
        World.Y = y;
        Projectiles.Clear();
        Clouds.Clear();
        World.Facing = facing ?? Enum.GetValues<Dir>().FirstOrDefault(d => World.Walkable(x + d.Step().X, y + d.Step().Y) && Here.At(x + d.Step().X, y + d.Step().Y) is not (Cell.StairsUp or Cell.StairsDown));
        bool first = !World.State.Visited;
        World.State.Visited = true;
        if (changed)
        {
            Say(depth == 0 ? "The Hall of Mirrors." : $"Level {depth}: {Here.Name}.", 0xFFFFD860);
            if (first && depth == Levels.Deepest) Play(Sfx.Roar);
        }
    }

    private void Fall()
    {
        Play(Sfx.Fall);
        Say("The floor gives way - the party falls!", 0xFFE08060);
        int depth = World.Depth + 1;
        int x = World.X, y = World.Y;
        Arrive(depth, x, y, World.Facing);
        if (!World.Walkable(x, y) || Here.At(x, y) is Cell.Pit)
        {
            // Land on the nearest solid floor.
            var best = (X: 1, Y: 1, D: int.MaxValue);
            for (int yy = 0; yy < Level.Size; yy++)
                for (int xx = 0; xx < Level.Size; xx++)
                    if (World.Walkable(xx, yy) && Here.At(xx, yy) == Cell.Floor && Math.Abs(xx - x) + Math.Abs(yy - y) < best.D)
                        best = (xx, yy, Math.Abs(xx - x) + Math.Abs(yy - y));
            World.X = best.X;
            World.Y = best.Y;
        }
        foreach (var c in World.Party.Where(c => c.Alive).ToList()) HurtChampion(c, Rng.Range(3, 9), quiet: true);
    }

    /// <summary>A plate or button: raises (or lowers) the gates it's wired to.</summary>
    private void Fire(int x, int y)
    {
        if (!Here.Triggers.TryGetValue((x, y), out var targets)) return;
        var st = World.State;
        foreach (var (tx, ty) in targets)
        {
            string key = LevelState.Key(tx, ty);
            if (st.Open.Contains(key) && Here.At(x, y) == Cell.Button) st.Open.Remove(key);
            else st.Open.Add(key);
        }
        Play(Sfx.Gate);
        if (st.Fired.Add(LevelState.Key(x, y))) Say("Somewhere nearby, stone grinds on stone.", 0xFFC0C0D0);
    }

    // ───────────────────────────── using things ─────────────────────────────

    /// <summary>Uses whatever is in front of the party: a door, a button, a fountain, a mirror, an altar, writing.</summary>
    public void Use()
    {
        var (fx, fy) = Ahead;
        var st = World.State;
        string key = LevelState.Key(fx, fy);
        switch (Here.At(fx, fy))
        {
            case Cell.Door:
                if (st.Open.Contains(key))
                {
                    if (CreatureAt(fx, fy) is not null) { Say("Something is standing in the doorway."); return; }
                    st.Open.Remove(key);
                }
                else st.Open.Add(key);
                Play(Sfx.Door);
                return;
            case Cell.Gate:
                Say(st.Open.Contains(key) ? "The gate is raised." : "A heavy gate. It won't lift by hand - something else must work it.");
                return;
            case Cell.Locked:
                if (st.Open.Contains(key)) return;
                string need = Here.Locks.GetValueOrDefault((fx, fy), "");
                var holder = World.Party.FirstOrDefault(c => c.Alive && c.Everything.Any(t => t.Id == need));
                if (holder is null)
                {
                    Say($"Locked. It needs the {(Items.Exists(need) ? Items.Get(need).Name : "right key")}.");
                    Play(Sfx.Bump);
                    return;
                }
                Take(holder, need);
                st.Open.Add(key);
                Play(Sfx.Unlock);
                Say($"{First(holder)} turns the {Items.Get(need).Name} in the lock. The door opens.", 0xFFFFD860);
                return;
            case Cell.RuneDoor:
                if (!st.Open.Contains(key)) Say("The door is sealed with glowing runes. Hands will not open it.", 0xFF80C0FF);
                return;
            case Cell.Button:
                Play(Sfx.Click);
                Fire(fx, fy);
                return;
            case Cell.Fountain:
                foreach (var c in World.Party.Where(c => c.Alive))
                {
                    c.Water = 100;
                    foreach (var t in c.Everything.Where(t => t.Item.Kind == ItemKind.Drink)) t.Charge = 3;
                }
                Play(Sfx.Drink);
                Say("The party drinks deep from the fountain, and fills its waterskins.", 0xFF80C0FF);
                return;
            case Cell.Writing:
                Tell("Words cut in the stone", Here.Writing.GetValueOrDefault((fx, fy), "The writing is too worn to read."));
                return;
            case Cell.Altar:
                Altar();
                return;
            case Cell.Mirror:
                Mirror(MirrorIndex(fx, fy));
                return;
        }
        Say("There's nothing there to use.");
    }

    /// <summary>What the party calls a champion: their given name ("Wenna", not "Old").</summary>
    public static string First(Champion c)
    {
        var words = c.Name.Split(' ');
        return words.Length > 1 && words[0] is "Old" or "Sister" ? words[1] : words[0];
    }

    /// <summary>Takes one of an item from wherever a champion carries it.</summary>
    public static bool Take(Champion c, string id)
    {
        if (c.Left?.Id == id) { c.Left = null; return true; }
        if (c.Right?.Id == id) { c.Right = null; return true; }
        if (c.Armour?.Id == id) { c.Armour = null; return true; }
        int i = c.Pack.FindIndex(t => t.Id == id);
        if (i < 0) return false;
        c.Pack.RemoveAt(i);
        return true;
    }

    private void Altar()
    {
        var dead = World.Party.Where(c => !c.Alive).ToList();
        if (dead.Count == 0)
        {
            Say("The altar is cold. It is for those who have fallen.");
            return;
        }
        foreach (var c in dead)
        {
            c.Health = Math.Max(1, c.MaxHealth / 2);
            c.Stamina = c.MaxStamina / 2;
            c.Food = Math.Max(c.Food, 50);
            c.Water = Math.Max(c.Water, 50);
            Say($"Light pours from the altar. {First(c)} draws breath again!", 0xFFFFE080);
        }
        Play(Sfx.Heal);
    }

    /// <summary>Picks up the top thing lying on the party's square.</summary>
    public void Grab()
    {
        var here = World.State.ThingsAt(World.X, World.Y);
        if (here.Count == 0)
        {
            Say("There's nothing here to pick up.");
            return;
        }
        var t = here[^1];
        var who = Receiver(t);
        if (who is null)
        {
            Say("Nobody has room to carry it.");
            return;
        }
        here.RemoveAt(here.Count - 1);
        Give(who, t);
        Play(Sfx.PickUp);
    }

    /// <summary>Who takes something picked up: a free hand for a torch or a weapon, else the emptiest pack.</summary>
    private Champion? Receiver(Thing t)
    {
        var living = World.Party.Where(c => c.Alive).ToList();
        if (t.Item.Kind == ItemKind.Torch || t.Item.IsWeapon)
        {
            var hand = living.FirstOrDefault(c => c.Left is null || c.Right is null && (t.Item.Kind == ItemKind.Torch || c.Weapon is null));
            if (hand is not null) return hand;
        }
        return living.Where(c => c.Pack.Count < Champion.PackSize).OrderBy(c => c.Pack.Count).FirstOrDefault();
    }

    private void Give(Champion c, Thing t)
    {
        string where;
        if ((t.Item.IsWeapon && c.Weapon is null || t.Item.Kind == ItemKind.Torch) && c.Right is null) { c.Right = t; where = "takes"; }
        else if ((t.Item.IsWeapon || t.Item.Kind is ItemKind.Torch or ItemKind.Shield) && c.Left is null) { c.Left = t; where = "takes"; }
        else { c.Pack.Add(t); where = "packs"; }
        Say($"{First(c)} {where} the {t.Item.Name}.", 0xFFB0C0FF);
        if (t.Item.Kind == ItemKind.Scroll) Learn(t.Item);
    }

    // ───────────────────────────── fighting ─────────────────────────────

    /// <summary>A champion uses their weapon (the first or second of what it can do).</summary>
    public void Attack(int index, int action)
    {
        if (index >= World.Party.Count) return;
        var c = World.Party[index];
        if (!c.Alive || c.Cooldown > 0) return;
        var actions = c.Actions.ToList();
        var a = actions[Math.Min(action, actions.Count - 1)];
        var (fx, fy) = Ahead;
        bool tired = c.Stamina < a.Stamina;
        double cooldown = a.Cooldown * (tired ? 1.6 : 1);

        if (a.Ranged)
        {
            Thing? missile;
            if (a.Name == "THROW") { missile = c.Weapon; if (missile is not null) Take(c, missile.Id); }
            else
            {
                string ammo = c.Weapon?.Item.Ammo ?? "";
                missile = c.Hands.Concat(c.Pack).FirstOrDefault(t => t.Id == ammo);
                if (missile is not null) Take(c, missile.Id);
            }
            if (missile is null)
            {
                Say($"{First(c)} has nothing to {a.Name.ToLowerInvariant()}.");
                return;
            }
            int dmg = (int)(a.Damage * (0.6 + c.Dexterity / 120.0 + c.Level(a.Skill) * 0.06) * (tired ? 0.5 : 1));
            Launch(new Projectile { X = World.X, Y = World.Y, Dir = World.Facing, Damage = dmg, Thing = missile, FromParty = true, Shooter = index, Skill = a.Skill });
            Play(Sfx.Swing);
        }
        else
        {
            if (!c.Front)
            {
                Say($"{First(c)} can't reach from the back row.");
                return;
            }
            var target = CreatureAt(fx, fy);
            Play(Sfx.Swing);
            if (target is not null)
            {
                if (a.Name == "BIND" && target.Kind == "vorthane")
                {
                    target.Bound = 6;
                    Say($"{First(c)} raises the Starheart: Vorthane is BOUND!", 0xFFFFE070);
                    Play(Sfx.Spell);
                    Learn(c, a.Skill, 40);
                }
                else
                {
                    int chance = Math.Clamp(60 + c.Dexterity / 2 + c.Level(a.Skill) * 4 - target.Info.Armour * 3, 15, 95);
                    if (Rng.Next(100) < chance)
                    {
                        bool hallowed = c.Weapon?.Item.Hallowed == true;
                        double roll = 0.75 + Rng.Next(51) / 100.0;
                        int dmg = (int)(a.Damage * (0.6 + c.Strength / 120.0 + c.Level(a.Skill) * 0.06) * roll * (tired ? 0.5 : 1)) - target.Info.Armour;
                        if (target.Info.Spirit && !hallowed)
                        {
                            Say($"The blow passes straight through the {target.Info.Name}!", 0xFF90D0FF);
                        }
                        else HurtCreature(target, Math.Max(1, dmg), c, a.Skill);
                    }
                    else Say($"{First(c)} misses.");
                }
            }
        }
        c.Cooldown = cooldown;
        c.Stamina = Math.Max(0, c.Stamina - a.Stamina);
    }

    /// <summary>A creature or trap hurts a champion (a ward turns half of it aside).</summary>
    public void HurtChampion(Champion c, int damage, bool quiet = false)
    {
        if (!c.Alive) return;
        if (World.Ward > 0) damage = Math.Max(1, damage / 2);
        c.Hurt(damage);
        Hurt = 0.25;
        if (!quiet) Play(Sfx.Hurt);
        if (c.Alive) return;
        Play(Sfx.Die);
        Say($"{c.Name} is dead!", 0xFFFF5040);
        // What they carried falls where they fell.
        foreach (var t in c.Everything.ToList()) World.State.Put(World.X, World.Y, t);
        c.Left = c.Right = c.Armour = null;
        c.Pack.Clear();
        c.Poison = 0;
    }

    /// <summary>Experience for a champion, with a cheer when a skill goes up a level.</summary>
    public void Learn(Champion c, Skill s, int xp)
    {
        if (c.Gain(s, xp) is { } level)
        {
            Play(Sfx.LevelUp);
            Say($"{First(c)} is now {(Champion.Ranks[level].StartsWith('A') || Champion.Ranks[level].StartsWith('E') ? "an" : "a")} {Champion.Ranks[level]} {s}!", 0xFF80FF80);
        }
    }
}
