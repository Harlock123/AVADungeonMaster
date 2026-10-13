using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

public sealed partial class GameEngine
{
    // ───────────────────────────── title ─────────────────────────────

    public void ShowTitle()
    {
        Mode = Mode.Title;
        Demo = null;
        Idle = 0;
        CloseAll();
        Projectiles.Clear();
        Clouds.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.TitleTune);
        var p = new Panel { Title = "" };
        p.Add("Enter the Hall of Mirrors", () => NewGame(quick: false), note: "Walk the hall and choose up to four champions from the mirrors.");
        p.Add("Quick start", () => NewGame(quick: true), note: "Begin on the first level with Brynja, Taric, Old Wenna and Ysmar.");
        p.Add("Continue a saved game", () => LoadMenu(), Saves.Any());
        p.Add("How to play", Help);
        p.Add("Quit", () => QuitRequested?.Invoke());
        Push(p);
    }

    public void NewGame(bool quick)
    {
        World = new WorldState { Spellbook = ["Light", "Mend"] };
        Log.Clear();
        CloseAll();
        Mode = Mode.Play;
        Caster = 0;
        Runes_.Clear();
        Play(Sfx.MusicStop);
        Play(Sfx.Ambience);
        var hall = Levels.Get(0);
        Arrive(0, hall.Start!.Value.X, hall.Start.Value.Y, Dir.North);
        if (quick)
        {
            var roster = Roster.Make();
            foreach (int i in new[] { 0, 1, 2, 3 }) Join(i, roster[i]);
            GoTo(1, down: true);
        }
        else Say("Walk up to a mirror and press Space to meet the champion inside.", 0xFFFFD860);
    }

    // ───────────────────────────── the mirrors ─────────────────────────────

    /// <summary>Which champion a mirror holds: the left wall top to bottom, then the right.</summary>
    public int MirrorIndex(int x, int y)
    {
        var mirrors = new List<(int, int)>();
        foreach (int col in new[] { 5, 9 })
            for (int yy = 0; yy < Level.Size; yy++)
                if (Levels.Get(0).At(col, yy) == Cell.Mirror) mirrors.Add((col, yy));
        return mirrors.IndexOf((x, y));
    }

    private void Mirror(int index)
    {
        if (index < 0) return;
        var c = Roster.Make()[index];
        if (World.Freed.Contains(index))
        {
            Say("The mirror is empty now: its champion walks with you.");
            return;
        }
        var p = new Panel { Title = c.Name, Kind = PanelKind.Mirror, Who = c, Back = Pop };
        p.Say($"In the glass stands {c.Title}. They press a hand against it from the other side.");
        if (World.Party.Count < 4) p.Add("Free them to join the party", () => { Pop(); Join(index, c); });
        else p.Say("Your party already has four.");
        p.Add("Leave them in the glass", Pop);
        Push(p);
    }

    private void Join(int index, Champion c)
    {
        c.Slot = World.Party.Count;
        World.Party.Add(c);
        // The first champion with any magic speaks the runes until told otherwise.
        if (World.Party[Caster].MaxMana == 0 && c.MaxMana > 0) Caster = World.Party.Count - 1;
        World.Freed.Add(index);
        Play(Sfx.Heal);
        Say($"{c.Name} steps out of the mirror and joins you.", 0xFF80FF80);
    }

    // ───────────────────────────── the pack ─────────────────────────────

    /// <summary>A champion's sheet and pack. Left and Right move between champions.</summary>
    public void OpenPack(int index)
    {
        if (World.Party.Count == 0) return;
        index = (index % World.Party.Count + World.Party.Count) % World.Party.Count;
        var c = World.Party[index];
        Show(() =>
        {
            var p = new Panel { Title = c.Name, Kind = PanelKind.Sheet, Who = c, Back = Pop };
            p.Turn = d => { Pop(); OpenPack(index + d); };
            void Slot(string label, Thing? t, Action<Thing?> set)
            {
                if (t is null) p.Add($"{label}: (empty)", null, false);
                else p.Add($"{label}: {Describe(t)}", () => ThingMenu(c, t, set), note: t.Item.Text.Length > 0 ? t.Item.Text : null);
            }
            Slot("Right hand", c.Right, t => c.Right = t);
            Slot("Left hand", c.Left, t => c.Left = t);
            Slot("Worn", c.Armour, t => c.Armour = t);
            for (int i = 0; i < c.Pack.Count; i++)
            {
                var t = c.Pack[i];
                p.Add($"Pack: {Describe(t)}", () => ThingMenu(c, t, n => { if (n is null) c.Pack.Remove(t); }), note: t.Item.Text.Length > 0 ? t.Item.Text : null);
            }
            if (World.Party.Count > 1 && c.Slot > 0)
            {
                var ahead = World.Party.First(o => o.Slot == c.Slot - 1);
                p.Add($"Change places with {First(ahead)}", () => { (ahead.Slot, c.Slot) = (c.Slot, ahead.Slot); World.Party.Sort((a, b) => a.Slot.CompareTo(b.Slot)); Pop(); OpenPack(c.Slot); });
            }
            p.Add("Done", Pop);
            return p;
        });
    }

    public static string Describe(Thing t) => t.Item.Kind switch
    {
        ItemKind.Torch => $"{t.Item.Name} ({(int)(t.Charge / 60)}m {(int)t.Charge % 60:00}s)",
        ItemKind.Drink => $"{t.Item.Name} ({(t.Charge <= 0 ? "empty" : $"{(int)t.Charge} drinks")})",
        _ => t.Item.Name,
    };

    /// <summary>What can be done with one thing: hold it, wear it, eat it, read it, give it, drop it.</summary>
    private void ThingMenu(Champion c, Thing t, Action<Thing?> clear)
    {
        var item = t.Item;
        Show(() =>
        {
            var p = new Panel { Title = item.Name, Kind = PanelKind.Sheet, Who = c, Back = Pop };
            if (item.Text.Length > 0) p.Say(item.Text);
            void Move(string label, Func<Thing?> get, Action<Thing?> set)
            {
                p.Add(label, () =>
                {
                    var old = get();
                    clear(null);
                    if (old is not null && old != t) c.Pack.Add(old);
                    set(t);
                    if (c.Pack.Count > Champion.PackSize) { World.State.Put(World.X, World.Y, c.Pack[^1]); c.Pack.RemoveAt(c.Pack.Count - 1); }
                    Pop();
                });
            }
            if (c.Right != t && item.Kind != ItemKind.Armour) Move("Hold in the right hand", () => c.Right, n => c.Right = n);
            if (c.Left != t && item.Kind != ItemKind.Armour) Move("Hold in the left hand", () => c.Left, n => c.Left = n);
            if (item.Kind == ItemKind.Armour && c.Armour != t) Move("Wear it", () => c.Armour, n => c.Armour = n);
            if (item.Kind == ItemKind.Food)
                p.Add($"Eat it (food {(int)c.Food}%)", () => { c.Food = Math.Min(100, c.Food + item.Food); clear(null); Play(Sfx.Eat); Say($"{First(c)} eats the {item.Name}."); Pop(); });
            if (item.Kind == ItemKind.Drink && t.Charge > 0)
                p.Add($"Drink (water {(int)c.Water}%)", () => { c.Water = Math.Min(100, c.Water + item.Water); t.Charge--; Play(Sfx.Drink); Pop(); });
            if (item.Kind == ItemKind.Potion)
                p.Add("Drink it", () =>
                {
                    c.Health = Math.Min(c.MaxHealth, c.Health + item.Heal);
                    c.Stamina = Math.Min(c.MaxStamina, c.Stamina + item.StaminaGain);
                    c.Mana = Math.Min(c.MaxMana, c.Mana + item.ManaGain);
                    if (item.Heal > 0) c.Poison = 0;
                    clear(null);
                    Play(Sfx.Drink);
                    Say($"{First(c)} drinks the {item.Name}.", 0xFF80FF80);
                    Pop();
                });
            if (item.Kind == ItemKind.Scroll) p.Add("Read it", () => { Learn(item); Pop(); Tell("The scroll", item.Text); });
            foreach (var other in World.Party.Where(o => o != c && o.Alive && o.Pack.Count < Champion.PackSize))
            {
                var to = other;
                p.Add($"Give it to {First(other)}", () => { clear(null); to.Pack.Add(t); Pop(); });
            }
            p.Add("Put it down here", () => { clear(null); World.State.Put(World.X, World.Y, t); Play(Sfx.Drop); Pop(); });
            p.Add("Back", Pop);
            return p;
        });
    }

    // ───────────────────────────── menus ─────────────────────────────

    public void GameMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "The dungeon waits", Back = Pop };
            p.Add("Carry on", Pop);
            p.Add("Look in a champion's pack", () => { Pop(); OpenPack(0); }, World.Party.Count > 0);
            p.Add("Save the game", SaveMenu, World.Party.Count > 0);
            p.Add("Load a game", () => LoadMenu());
            p.Add("How to play", Help);
            p.Add("Quit to the title", () =>
            {
                var q = new Panel { Title = "Quit to the title?", Back = Pop };
                q.Say("Anything not saved will be lost.");
                q.Add("Yes, quit", ShowTitle);
                q.Add("No, carry on", Pop);
                q.Selected = 1;
                Push(q);
            });
            return p;
        });
    }

    public void SaveMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Save the game", Back = Pop };
            for (int slot = 1; slot <= SaveStore.Slots; slot++)
            {
                int s = slot;
                p.Add(Saves.Describe(s), () => SaveTo(s));
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public void SaveTo(int slot)
    {
        World.Dice = Rng.State;
        try
        {
            Saves.Save(slot, World);
            CloseAll();
            Say($"Saved in slot {slot}.", 0xFF80FF80);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Tell("Not saved", "The game could not be saved: " + e.Message);
        }
    }

    public void LoadMenu()
    {
        Show(() =>
        {
            var p = new Panel { Title = "Load a game", Back = Pop };
            for (int slot = 1; slot <= SaveStore.Slots; slot++)
            {
                int s = slot;
                string text = Saves.Describe(s);
                p.Add(text, () => LoadFrom(s), !text.EndsWith("empty"));
            }
            p.Add("Cancel", Pop);
            return p;
        });
    }

    public bool LoadFrom(int slot)
    {
        var w = Saves.Load(slot);
        if (w is null) return false;
        World = w;
        Rng.State = w.Dice;
        CloseAll();
        Log.Clear();
        Projectiles.Clear();
        Clouds.Clear();
        Runes_.Clear();
        Caster = 0;
        Demo = null;
        Mode = Mode.Play;
        Play(Sfx.MusicStop);
        Play(Sfx.Ambience);
        Say($"Loaded slot {slot}. {Here.Name}.", 0xFFFFD860);
        return true;
    }

    public void Help()
    {
        var p = new Panel { Title = "How to play", Back = Pop };
        p.Say("MOVING: Up and Down walk, Left and Right turn, Q and E step sideways. Space uses what's in front of you: a door, a button, a fountain, an altar, a mirror, writing on the wall. G picks up what's lying where you stand.");
        p.Say("FIGHTING: Z X C V make champions 1-4 use their weapon; A S D F use its second action (a thrust, a throw). Only the front two can reach a foe with a blade. Each champion needs a moment to recover after acting.");
        p.Say("MAGIC: Tab chooses who casts. Speak runes with 1-6: first a power rune (IN to THAN), then an element, then perhaps a form. Enter casts, Backspace takes back a rune. Scrolls teach the right words.");
        p.Say("THE PARTY: I opens a champion's pack (Left/Right for the next): hold, wear, eat, drink, read, give and drop things. Keep everyone fed and watered, carry torches, and rest by simply waiting. Esc opens the menu: save, load, quit.");
        p.Add("Back", Pop);
        Push(p);
    }

    // ───────────────────────────── endings ─────────────────────────────

    private void PartyDead()
    {
        Mode = Mode.Dead;
        Play(Sfx.MusicStop);
        Play(Sfx.Defeat);
        CloseAll();
        var p = new Panel { Title = "The party has fallen" };
        p.Say("The Deepvault keeps its champions. Somewhere above, twelve mirrors darken.");
        p.Add("Load a saved game", () => LoadMenu(), Saves.Any());
        p.Add("Return to the title", ShowTitle);
        Push(p);
    }

    private void Victory()
    {
        World.Won = true;
        Mode = Mode.Won;
        Play(Sfx.MusicStop);
        Play(Sfx.Victory);
        CloseAll();
        int minutes = (int)(World.Time / 60);
        var p = new Panel { Title = "Vorthane is unmade", Kind = PanelKind.Ending };
        p.Say("The Unmade comes apart like smoke in a wind, and the Starheart blazes white. Far above, in the Hall, every mirror cracks at once - and the champions still bound in them step out, blinking, into the torchlight.");
        p.Say($"{string.Join(", ", World.Party.Where(c => c.Alive).Select(c => c.Name))} climb back up into the day. It took them {minutes / 60} hours and {minutes % 60} minutes, and {World.Kills} creatures fell to them.");
        p.Add("Return to the title", ShowTitle);
        Push(p);
    }

    // ───────────────────────────── the attract demo ─────────────────────────────

    public void StartDemo()
    {
        NewGame(quick: true);
        foreach (var c in World.Party) c.Health = c.MaxHealth;
        Demo = new DemoPilot(this);
    }

    public void EndDemo()
    {
        Demo = null;
        World = new WorldState();
        ShowTitle();
    }
}
