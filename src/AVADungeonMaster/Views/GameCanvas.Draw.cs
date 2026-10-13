using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVADungeonMaster.Game;
using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Views;

public sealed partial class GameCanvas
{
    // The layout of the exploring screen.
    private static readonly Rect View = new(16, 106, 640, 420);
    private static readonly Rect Side = new(672, 106, 336, 420);
    private static readonly Rect Bottom = new(16, 534, 992, 218);

    private static readonly Typeface Retro = new(new FontFamily("avares://AVADungeonMaster/Assets/Fonts#VT323"));
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width);
        return p;
    }

    private const uint Ground = 0xFF0C0A10, Panel = 0xFF16121C, Frame = 0xFFB8963C, Ink = 0xFFE8E0D0, Gold = 0xFFFFD860,
        Dim = 0xFF8A8278, Good = 0xFF60D080, Warn = 0xFFE0C040, Bad = 0xFFE05040, Blue = 0xFF80B0FF;

    private static FormattedText Fmt(string text, double size, uint colour) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));

    private static double Measure(string text, double size) => Fmt(text, size, Ink).Width;

    /// <summary>Text with its top-left corner at a point (or centred, or right-aligned, on it).</summary>
    private static void Text(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        var ft = Fmt(text, size, colour);
        double left = align == 0 ? x : align == 1 ? x - ft.Width / 2 : x - ft.Width;
        dc.DrawText(ft, new Point(left, y));
    }

    private static void Shadowed(DrawingContext dc, string text, double x, double y, double size, uint colour, int align = 0)
    {
        Text(dc, text, x + 2, y + 2, size, 0xC0000000, align);
        Text(dc, text, x, y, size, colour, align);
    }

    private static readonly Dictionary<(string, double, double), List<string>> WrapCache = [];

    /// <summary>Splits text into lines that fit a width.</summary>
    private static List<string> Wrap(string text, double width, double size)
    {
        if (WrapCache.TryGetValue((text, width, size), out var cached)) return cached;
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string tryLine = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(tryLine, size) > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = tryLine;
        }
        if (line.Length > 0) lines.Add(line);
        if (WrapCache.Count > 2000) WrapCache.Clear();
        WrapCache[(text, width, size)] = lines;
        return lines;
    }

    private static void Box(DrawingContext dc, Rect r, uint fill = Panel, uint border = Frame)
    {
        dc.FillRectangle(Brush(fill), r);
        dc.DrawRectangle(Pen(border, 2), r.Deflate(1));
        dc.DrawRectangle(Pen(Sprites.Shade(border, 0.45), 1), r.Deflate(4));
    }

    /// <summary>Paints a 16×16 drawing, merging runs of one colour into single rectangles.</summary>
    private static void Sprite(DrawingContext dc, string name, uint tint, double x, double y, double scale, double alpha = 1, bool flip = false)
    {
        if (!Sprites.All.TryGetValue(name, out var rows)) return;
        for (int r = 0; r < rows.Length; r++)
        {
            string row = flip ? new string(rows[r].Reverse().ToArray()) : rows[r];
            int c = 0;
            while (c < row.Length)
            {
                uint col = Sprites.Colour(row[c], tint);
                int start = c;
                while (c < row.Length && Sprites.Colour(row[c], tint) == col) c++;
                if (col == 0) continue;
                if (alpha < 1) col = (col & 0x00FFFFFF) | ((uint)(alpha * 255) << 24);
                dc.FillRectangle(Brush(col), new Rect(x + start * scale, y + r * scale, (c - start) * scale + 0.4, scale + 0.4));
            }
        }
    }


    /// <summary>The game's clock, for everything that flickers, glows or blinks.</summary>
    private double _clock => Engine.Clock;

    private static uint Mix(uint a, uint b, double t)
    {
        uint C(int s) => (uint)Math.Round(((a >> s) & 0xFF) * (1 - t) + ((b >> s) & 0xFF) * t);
        return 0xFF000000 | (C(16) << 16) | (C(8) << 8) | C(0);
    }

    private void DrawScreen(DrawingContext dc)
    {
        dc.FillRectangle(Brush(Ground), new Rect(0, 0, ScreenWidth, ScreenHeight));
        var g = Engine;
        var top = g.Top;
        if (g.Mode == Mode.Title)
        {
            DrawTitle(dc);
            if (top is not null)
            {
                bool menu = g.Panels.Count == 1;
                DrawPanel(dc, menu ? new Rect(312, 420, 400, 230) : new Rect(112, 300, 800, 440), top);
            }
            if (g.InDemo) DrawPlay(dc);
            return;
        }
        DrawPlay(dc);
        if (top is { Kind: PanelKind.Ending } || g.Mode is Mode.Dead or Mode.Won)
        {
            dc.FillRectangle(Brush(0xC0000000), new Rect(0, 0, ScreenWidth, ScreenHeight));
            if (top is not null) DrawPanel(dc, new Rect(112, 220, 800, 360), top);
        }
        else if (top is not null) DrawPanel(dc, Bottom, top);
        if (g.InDemo)
        {
            double pulse = 0.6 + 0.4 * Math.Sin(g.Clock * 4);
            dc.FillRectangle(Brush(0xC0000000), new Rect(176, 290, 320, 50));
            Text(dc, "DEMO - PRESS ANY KEY", 336, 298, 32, Sprites.Shade(Gold, pulse), 1);
        }
    }

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        var top = g.Top;
        DrawChampions(dc);
        Box(dc, View.Inflate(4), 0xFF000000);
        using (dc.PushClip(View))
        {
            if (top is { Kind: PanelKind.Sheet, Who: { } who }) DrawSheet(dc, View, who);
            else if (top is { Kind: PanelKind.Mirror, Who: { } face }) DrawMirror(dc, View, face);
            else Draw3D(dc, View);
            if (top is null)
            {
                dc.FillRectangle(Brush(0x90000000), new Rect(View.X, View.Bottom - 26, View.Width, 26));
                Text(dc, g.World.Depth == 0 ? g.Here.Name : $"Level {g.World.Depth}: {g.Here.Name}", View.X + 10, View.Bottom - 25, 22, Gold);
                Text(dc, $"facing {g.World.Facing}", View.Right - 10, View.Bottom - 25, 22, Ink, 2);
            }
        }
        DrawSide(dc);
        if (top is null) DrawLog(dc);
    }

    // ───────────────────────────── the champions ─────────────────────────────

    private void DrawChampions(DrawingContext dc)
    {
        var party = Engine.World.Party;
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect(16 + i * 250, 6, 242, 92);
            if (i >= party.Count)
            {
                Box(dc, r, 0xFF0E0C12, 0xFF3A3428);
                Text(dc, "(an empty place)", r.Center.X, r.Y + 34, 22, 0xFF4A443C, 1);
                continue;
            }
            var c = party.First(p => p.Slot == i);
            bool caster = Engine.Caster == party.IndexOf(c);
            Box(dc, r, c.Alive ? 0xFF16121C : 0xFF100E10, caster ? Gold : Frame);
            if (!c.Alive)
            {
                Text(dc, GameEngine.First(c), r.X + 12, r.Y + 6, 26, Dim);
                Text(dc, "DEAD", r.Center.X, r.Y + 44, 32, Bad, 1);
                continue;
            }
            Sprite(dc, Portrait(c), c.Colour, r.X + 8, r.Y + 22, 3);
            Text(dc, GameEngine.First(c), r.X + 10, r.Y + 2, 24, Sprites.Shade(c.Colour, 1.3));
            Text(dc, (c.Slot < 2 ? "FRONT " : "BACK ") + (i + 1), r.Right - 10, r.Y + 4, 18, Dim, 2);
            void Bar(double y, double v, double max, uint col)
            {
                var bar = new Rect(r.X + 58, y, 96, 9);
                dc.FillRectangle(Brush(0xFF0A080C), bar);
                dc.FillRectangle(Brush(col), new Rect(bar.X, bar.Y, bar.Width * Math.Clamp(v / Math.Max(1, max), 0, 1), bar.Height));
            }
            Bar(r.Y + 32, c.Health, c.MaxHealth, 0xFFD04040);
            Bar(r.Y + 46, c.Stamina, c.MaxStamina, 0xFFD0B040);
            Bar(r.Y + 60, c.Mana, c.MaxMana, 0xFF4070E0);
            Text(dc, $"{c.Health}", r.X + 158, r.Y + 27, 16, Ink);
            Text(dc, $"{(int)c.Stamina}", r.X + 158, r.Y + 41, 16, Ink);
            Text(dc, c.MaxMana > 0 ? $"{(int)c.Mana}" : "-", r.X + 158, r.Y + 55, 16, Ink);
            // The two hands.
            for (int hnd = 0; hnd < 2; hnd++)
            {
                var t = hnd == 0 ? c.Left : c.Right;
                var box = new Rect(r.Right - 54 + hnd * 26, r.Y + 32, 24, 24);
                dc.FillRectangle(Brush(0xFF0A080C), box);
                dc.DrawRectangle(Pen(0xFF3A3428), box);
                if (t is not null) DrawIcon(dc, t.Item, box.Center, 22);
            }
            string state = c.Poison > 0 ? "POISONED" : c.Food < 15 ? "HUNGRY" : c.Water < 15 ? "THIRSTY" : "";
            if (state.Length > 0) Text(dc, state, r.X + 58, r.Y + 70, 18, Bad);
            if (c.Cooldown > 0) dc.FillRectangle(Brush(0x80808080), new Rect(r.X + 4, r.Bottom - 6, (r.Width - 8) * Math.Min(1, c.Cooldown / 2), 3));
        }
    }

    // ───────────────────────────── magic and weapons ─────────────────────────────

    private void DrawSide(DrawingContext dc)
    {
        var g = Engine;
        var r = Side;
        Box(dc, r);
        double x = r.X + 14, y = r.Y + 8;
        var party = g.World.Party;
        Text(dc, "MAGIC", x, y, 26, Gold);
        if (party.Count == 0)
        {
            Text(dc, "No champions yet.", x, y + 34, 22, Dim);
            return;
        }
        var caster = party[Math.Min(g.Caster, party.Count - 1)];
        Text(dc, $"{GameEngine.First(caster)}  mana {(int)caster.Mana}/{caster.MaxMana}", r.Right - 14, y + 4, 20, Blue, 2);
        y += 32;
        string spoken = g.Spoken.Count == 0 ? "-" : string.Join(" ", g.Spoken);
        dc.FillRectangle(Brush(0xFF0A080C), new Rect(x - 4, y, r.Width - 20, 30));
        Text(dc, spoken, x + 4, y + 2, 26, 0xFFFFE8A0);
        if (g.Spoken.Count > 0) Text(dc, $"cost {Runes.Cost(g.Spoken)}", r.Right - 18, y + 6, 20, Dim, 2);
        y += 36;
        if (g.Spoken.Count < 4)
        {
            var row = Runes.Row(g.Spoken.Count);
            string label = g.Spoken.Count == 0 ? "POWER" : g.Spoken.Count == 1 ? "ELEMENT" : "FORM";
            Text(dc, label, x, y, 20, Dim);
            for (int i = 0; i < row.Length; i++) Text(dc, $"{i + 1} {row[i]}", x + 76 + (i % 3) * 82, y + (i / 3) * 22, 21, Ink);
            y += 48;
        }
        Text(dc, "Enter cast  Back undo  Tab caster", x, y, 19, 0xFF6A6258);
        y += 24;
        foreach (var name in g.World.Spellbook.Take(5))
        {
            var s = Spell.All.First(sp => sp.Name == name);
            Text(dc, $"{s.Name,-12} (power) {s.Runes}", x, y, 19, Blue);
            y += 19;
        }

        // Weapons.
        y = r.Y + 252;
        dc.DrawLine(Pen(Frame), new Point(r.X + 8, y - 6), new Point(r.Right - 8, y - 6));
        Text(dc, "ARMS", x, y, 26, Gold);
        y += 30;
        string[] keys = ["Z/A", "X/S", "C/D", "V/F"];
        for (int i = 0; i < party.Count; i++)
        {
            var c = party[i];
            var acts = c.Actions.ToList();
            uint col = !c.Alive ? 0xFF4A443C : c.Cooldown > 0 ? Dim : Ink;
            Text(dc, keys[i], x, y, 20, Gold);
            Text(dc, GameEngine.First(c), x + 40, y, 20, col);
            Text(dc, string.Join(" / ", acts.Select(a => a.Name)), r.Right - 14, y, 20, col, 2);
            if (c.Alive && c.Cooldown > 0)
                dc.FillRectangle(Brush(0x90808080), new Rect(x + 40, y + 21, (r.Width - 70) * Math.Min(1, c.Cooldown / 2), 3));
            y += 30;
        }
    }

    private void DrawLog(DrawingContext dc)
    {
        var r = Bottom;
        Box(dc, r);
        var log = Engine.Log;
        var shown = log.Skip(Math.Max(0, log.Count - 7)).ToList();
        for (int i = 0; i < shown.Count; i++)
        {
            var (text, colour, age) = shown[i];
            double fade = Math.Clamp(1 - (age - 8) / 10, 0.35, 1);
            Text(dc, text, r.X + 16, r.Y + 8 + i * 25, 22, Sprites.Shade(colour, fade));
        }
        Text(dc, "Arrows walk   Space use   G take   Z-V / A-F fight   1-6 runes   I pack   Esc menu   H help", r.X + r.Width / 2, r.Bottom - 28, 21, 0xFF6A6258, 1);
    }

    // ───────────────────────────── panels ─────────────────────────────

    private void DrawPanel(DrawingContext dc, Rect r, Game.Panel p)
    {
        Box(dc, r);
        double x = r.X + 18, width = r.Width - 36, y = r.Y + 10;
        const double size = 23, lh = 24;
        if (p.Title.Length > 0)
        {
            Text(dc, p.Title, x, y, 28, Gold);
            y += 34;
        }
        foreach (var para in p.Text)
        {
            foreach (var line in Wrap(para, width, size))
            {
                if (y > r.Bottom - lh * 2) break;
                Text(dc, line, x, y, size, Ink);
                y += lh;
            }
            y += 4;
        }
        string? note = p.Options.Count > 0 ? p.Options[p.Selected].Note : null;
        double noteSpace = note is null ? 0 : lh * Math.Min(2, Wrap(note, width, 21).Count) + 6;
        int room = Math.Max(1, (int)((r.Bottom - 10 - noteSpace - y) / lh));
        int first = Math.Clamp(p.Selected - room / 2, 0, Math.Max(0, p.Options.Count - room));
        for (int i = first; i < Math.Min(p.Options.Count, first + room); i++)
        {
            var o = p.Options[i];
            bool sel = i == p.Selected;
            if (sel) dc.FillRectangle(Brush(0xFF3A2E1C), new Rect(x - 6, y, width + 12, lh));
            uint col = !o.Enabled ? 0xFF5A544C : sel ? Gold : Ink;
            Text(dc, $"{(i < 9 ? $"{i + 1}." : "  ")} {o.Label}", x, y, size, col);
            y += lh;
        }
        if (first > 0) Text(dc, "more above", r.Right - 18, r.Y + 14, 20, Dim, 2);
        if (first + room < p.Options.Count) Text(dc, "more below", r.Right - 18, r.Bottom - 26, 20, Dim, 2);
        if (note is not null)
        {
            double ny = r.Bottom - noteSpace - 4;
            foreach (var line in Wrap(note, width, 21).Take(2))
            {
                Text(dc, line, x, ny, 21, Blue);
                ny += lh;
            }
        }
    }

    private void DrawSheet(DrawingContext dc, Rect r, Champion c)
    {
        dc.FillRectangle(Brush(0xFF120E16), r);
        double x = r.X + 18, y = r.Y + 12;
        Sprite(dc, Portrait(c), c.Colour, r.Right - 150, r.Y + 20, 8);
        Text(dc, c.Name, x, y, 34, Sprites.Shade(c.Colour, 1.3));
        Text(dc, c.Title, x, y + 36, 22, Dim);
        y += 72;
        Text(dc, $"Health {c.Health}/{c.MaxHealth}   Stamina {(int)c.Stamina}/{c.MaxStamina}   Mana {(int)c.Mana}/{c.MaxMana}", x, y, 22, Ink);
        y += 26;
        Text(dc, $"Strength {c.Strength}  Dexterity {c.Dexterity}  Wisdom {c.Wisdom}  Vitality {c.Vitality}", x, y, 22, Ink);
        y += 26;
        Text(dc, $"Armour {c.ArmourValue}   Food {(int)c.Food}%   Water {(int)c.Water}%{(c.Poison > 0 ? "   POISONED" : "")}", x, y, 22, c.Food < 20 || c.Water < 20 ? Warn : Ink);
        y += 36;
        foreach (Skill s in Enum.GetValues<Skill>())
        {
            int lvl = c.Level(s);
            string rank = lvl == 0 ? "-" : c.Rank(s);
            Text(dc, $"{s,-8} {rank}", x, y, 22, lvl == 0 ? Dim : Good);
            int next = lvl < Champion.LevelXp.Length ? Champion.LevelXp[lvl] : 0;
            Text(dc, lvl < Champion.LevelXp.Length ? $"{c.Xp[(int)s]} / {next}" : $"{c.Xp[(int)s]}", x + 300, y, 20, Dim);
            y += 24;
        }
        Text(dc, "Left/Right: next champion", r.Right - 12, r.Bottom - 28, 20, 0xFF6A6258, 2);
    }

    private void DrawMirror(DrawingContext dc, Rect r, Champion c)
    {
        dc.FillRectangle(Brush(0xFF0A0A12), r);
        var frame = new Rect(r.X + 30, r.Y + 20, 220, 300);
        dc.FillRectangle(Brush(0xFFB8963C), frame);
        dc.FillRectangle(Brush(0xFF5A6A88), frame.Deflate(12));
        Sprite(dc, Portrait(c), c.Colour, frame.X + 30, frame.Y + 60, 10);
        double x = r.X + 280, y = r.Y + 24;
        Text(dc, c.Name, x, y, 30, Sprites.Shade(c.Colour, 1.3));
        Text(dc, c.Title, x, y + 34, 21, Dim);
        y += 70;
        Text(dc, $"Health {c.MaxHealth}  Stamina {c.MaxStamina}  Mana {c.MaxMana}", x, y, 21, Ink);
        Text(dc, $"Str {c.Strength}  Dex {c.Dexterity}  Wis {c.Wisdom}  Vit {c.Vitality}", x, y + 24, 21, Ink);
        y += 60;
        foreach (Skill s in Enum.GetValues<Skill>())
            if (c.Level(s) > 0) { Text(dc, $"{c.Rank(s)} {s}", x, y, 22, Good); y += 24; }
        y += 10;
        Text(dc, "Carries: " + string.Join(", ", c.Everything.Select(t => t.Item.Name)), x, y, 20, Dim);
    }

    // ───────────────────────────── the title ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        for (int i = 0; i < 48; i++)
            dc.FillRectangle(Brush(Mix(0xFF050408, 0xFF1A1426, i / 47.0)), new Rect(0, i * 16, ScreenWidth, 17));
        // A great stone gate in the mountain, torches either side, darkness beyond.
        var gate = new Rect(352, 230, 320, 400);
        dc.FillRectangle(Brush(0xFF2A2630), new Rect(gate.X - 60, gate.Y - 40, gate.Width + 120, gate.Height + 60));
        for (int r = 0; r < 12; r++)
            for (int c = 0; c < 7; c++)
            {
                var b = new Rect(gate.X - 60 + c * 62 + (r % 2) * 31, gate.Y - 40 + r * 38, 60, 36);
                dc.DrawRectangle(Pen(0xFF1A1820), b);
            }
        dc.FillRectangle(Brush(0xFF000000), gate);
        for (int i = 0; i < 9; i++)
            dc.FillRectangle(Brush(0xFF3A3A44), new Rect(gate.X + 10 + i * 36, gate.Y, 8, 90));
        foreach (double tx in new[] { gate.X - 40, gate.Right + 40 })
        {
            double fl = 0.8 + 0.2 * Math.Sin(_clock * 9 + tx);
            for (int k = 3; k >= 1; k--) dc.DrawEllipse(Brush((uint)(20 * fl) << 24 | 0x00FFA040), null, new Point(tx, gate.Y + 60), 40 * k, 40 * k);
            dc.FillRectangle(Brush(0xFF3A2A1A), new Rect(tx - 4, gate.Y + 64, 8, 30));
            dc.DrawEllipse(Brush(0xFFFF9020), null, new Point(tx, gate.Y + 52), 9, 16 * fl);
            dc.DrawEllipse(Brush(0xFFFFE080), null, new Point(tx, gate.Y + 56), 4, 8 * fl);
        }
        // Eyes in the dark.
        if ((int)(_clock * 0.7) % 3 != 0)
            foreach (double ex in new[] { 488.0, 520 })
                dc.DrawEllipse(Brush(0xFFB060FF), null, new Point(ex, 420), 5, 3);
        Shadowed(dc, "AVADUNGEONMASTER", 512, 60, 88, 0xFFFFD860, 1);
        Shadowed(dc, "The Deepvault of Vorthane the Unmade", 512, 150, 34, 0xFFC0B0E0, 1);
        Text(dc, "A real-time dungeon of rune magic and four champions. Every name, place and creature here is new.", 512, 740, 21, 0xFF6A6258, 1);
    }
}
