using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Game;

public enum Mode { Title, Play, Dead, Won }

/// <summary>
/// The whole game, apart from drawing it. The dungeon runs in real time on a fixed 60 Hz clock:
/// creatures move and strike on their own timers, torches burn down, the party grows hungry. Keys
/// arrive by name ("Up", "Z", "Enter"), so tests and the attract demo play it just as a person does.
/// While a panel is open (a pack, a mirror, the menu) the dungeon waits.
/// </summary>
public sealed partial class GameEngine
{
    public const double Step = 1.0 / 60;
    public const double DemoAfter = 30;
    public const double DemoLength = 60;

    public Mode Mode { get; private set; } = Mode.Title;
    public WorldState World { get; private set; } = new();
    public Rng Rng { get; }
    public SaveStore Saves { get; }
    public List<Panel> Panels { get; } = [];
    public Panel? Top => Panels.Count > 0 ? Panels[^1] : null;
    public List<(string Text, uint Colour, double Age)> Log { get; } = [];
    public double Clock { get; private set; }
    public double Idle { get; private set; }
    public DemoPilot? Demo { get; private set; }
    public bool InDemo => Demo is not null;
    /// <summary>A flash of red over the view when the party is hit, or a bump when it walks into a wall.</summary>
    public double Hurt { get; private set; }
    public double Bumped { get; private set; }
    public event Action<Sfx>? Sound;
    public event Action? QuitRequested;
    private double _accumulator;

    public GameEngine(int seed, SaveStore? saves = null)
    {
        Rng = new Rng((ulong)seed * 2654435761UL + 1);
        Saves = saves ?? new SaveStore();
        ShowTitle();
    }

    public void Play(Sfx s) => Sound?.Invoke(s);

    public Level Here => World.Here;

    public void Say(string text, uint colour = 0xFFE8E0D0)
    {
        Log.Add((text, colour, 0));
        if (Log.Count > 40) Log.RemoveAt(0);
    }

    // ───────────────────────────── the clock ─────────────────────────────

    public void Update(double dt)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Step)
        {
            _accumulator -= Step;
            Tick();
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Tick()
    {
        Clock += Step;
        if (Hurt > 0) Hurt -= Step;
        if (Bumped > 0) Bumped -= Step;
        for (int i = 0; i < Log.Count; i++) Log[i] = (Log[i].Text, Log[i].Colour, Log[i].Age + Step);
        if (Demo is not null)
        {
            Demo.Update(Step);
            if (Demo.Elapsed > DemoLength || Mode == Mode.Dead) { EndDemo(); return; }
        }
        else if (Mode == Mode.Title && Panels.Count == 1)
        {
            Idle += Step;
            if (Idle > DemoAfter) StartDemo();
        }
        if (Mode == Mode.Play && Top is null) Live(Step);
    }

    // ───────────────────────────── panels ─────────────────────────────

    public Panel Push(Panel p)
    {
        Panels.Add(p);
        return p;
    }

    public Panel Show(Func<Panel> make)
    {
        var p = make();
        p.Make = make;
        return Push(p);
    }

    public void Pop()
    {
        if (Panels.Count > 0) Panels.RemoveAt(Panels.Count - 1);
        Refresh();
    }

    public void CloseAll() => Panels.Clear();

    public void Refresh()
    {
        if (Top is not { Make: { } make } old) return;
        var p = make();
        p.Make = make;
        p.Selected = Math.Clamp(old.Selected, 0, Math.Max(0, p.Options.Count - 1));
        Panels[^1] = p;
    }

    public void Tell(string title, string text, Action? then = null)
    {
        var p = new Panel { Title = title }.Say(text);
        p.Add("Continue", () => { Pop(); then?.Invoke(); });
        p.Back = () => { Pop(); then?.Invoke(); };
        Push(p);
    }

    // ───────────────────────────── keys ─────────────────────────────

    public void Press(string key)
    {
        if (Demo is not null)
        {
            EndDemo();
            return;
        }
        Input(key);
    }

    internal void Input(string key)
    {
        Idle = 0;
        if (Top is { } p)
        {
            PanelKey(p, key);
            return;
        }
        if (Mode == Mode.Play) PlayKey(key);
    }

    public void Type(string text)
    {
        if (Top is not { Entry: not null } p) return;
        foreach (char ch in text)
            if ((char.IsLetterOrDigit(ch) || ch == ' ') && p.Entry!.Length < 14) p.Entry += ch;
    }

    private void PanelKey(Panel p, string key)
    {
        var opts = p.Options;
        switch (key)
        {
            case "Up":
                if (opts.Count > 0) { p.Selected = (p.Selected + opts.Count - 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Down":
                if (opts.Count > 0) { p.Selected = (p.Selected + 1) % opts.Count; Play(Sfx.Menu); }
                return;
            case "Left" or "Right":
            {
                int d = key == "Left" ? -1 : 1;
                if (opts.Count > 0 && opts[p.Selected].Adjust is { } adj) { adj(d); Refresh(); }
                else p.Turn?.Invoke(d);
                Play(Sfx.Menu);
                return;
            }
            case "Enter" or "Space":
                Choose(p, p.Selected);
                return;
            case "Esc" or "Back":
                p.Back?.Invoke();
                return;
        }
        if (key.Length == 1 && char.IsDigit(key[0]) && key[0] != '0')
        {
            int n = key[0] - '1';
            if (n < opts.Count) { p.Selected = n; Choose(p, n); }
        }
    }

    private void Choose(Panel p, int index)
    {
        if (index < 0 || index >= p.Options.Count) return;
        var o = p.Options[index];
        if (!o.Enabled || o.Act is null) return;
        Play(Sfx.Pick);
        o.Act();
        if (Top == p) Refresh();
    }

    private void PlayKey(string key)
    {
        switch (key)
        {
            case "Up": Move(World.Facing); return;
            case "Down": Move(World.Facing.Back()); return;
            case "Left": Turn(World.Facing.Left()); return;
            case "Right": Turn(World.Facing.Right()); return;
            case "Q": Move(World.Facing.Left()); return;
            case "E": Move(World.Facing.Right()); return;
            case "Z": Attack(0, 0); return;
            case "X": Attack(1, 0); return;
            case "C": Attack(2, 0); return;
            case "V": Attack(3, 0); return;
            case "A": Attack(0, 1); return;
            case "S": Attack(1, 1); return;
            case "D": Attack(2, 1); return;
            case "F": Attack(3, 1); return;
            case "Tab": NextCaster(); return;
            case "Enter": Cast(); return;
            case "Back": UnRune(); return;
            case "Space": Use(); return;
            case "G": Grab(); return;
            case "I": OpenPack(0); return;
            case "H": Help(); return;
            case "Esc": GameMenu(); return;
        }
        if (key.Length == 1 && key[0] is >= '1' and <= '6') Rune(key[0] - '1');
    }
}
