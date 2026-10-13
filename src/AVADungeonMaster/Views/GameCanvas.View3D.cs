using Avalonia;
using Avalonia.Media;
using AVADungeonMaster.Game;
using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Views;

public sealed partial class GameCanvas
{
    private const double Focal = 200;
    private static double HW(double b) => Focal / (b + 1);
    private static double HH(double b) => HW(b) * 0.85;

    /// <summary>
    /// The first-person view: five squares deep at most, less in the dark. Painted far to near: the
    /// floor and ceiling tiles, then at each depth the side walls, the faces that look straight at the
    /// party, and whatever stands, lies or flies in those squares.
    /// </summary>
    private void Draw3D(DrawingContext dc, Rect v)
    {
        var g = Engine;
        var w = g.World;
        var lv = w.Here;
        double cx = v.X + v.Width / 2, cy = v.Y + v.Height / 2 + 6;
        int depth = g.ViewDepth;
        double light = Math.Clamp(g.Light / 1.6, 0.25, 1.0);
        dc.FillRectangle(Brush(0xFF000000), v);
        uint floor = Sprites.Parse(lv.FloorColour), ceiling = Sprites.Parse(lv.CeilingColour), wallCol = Sprites.Parse(lv.WallColour), altCol = Sprites.Parse(lv.AltColour);
        var f = w.Facing.Step();
        var rt = w.Facing.Right().Step();
        int[] laterals = [-3, 3, -2, 2, -1, 1, 0];
        double Shade(int k) => Math.Max(0, 1 - (double)k / (depth + 0.6)) * light;

        // Floor and ceiling squares.
        for (int k = depth; k >= 0; k--)
        {
            double nb = k == 0 ? -0.55 : k - 1, fb = k, shade = Shade(k);
            foreach (int l in laterals)
            {
                int mx = w.X + k * f.X + l * rt.X, my = w.Y + k * f.Y + l * rt.Y;
                if (!Walkable(w, mx, my)) continue;
                uint h = Hash(lv.Number, mx, my);
                double jitter = 0.9 + (h % 13) / 100.0;
                var a = new Point(cx + (2 * l - 1) * HW(fb), cy + HH(fb));
                var b = new Point(cx + (2 * l + 1) * HW(fb), cy + HH(fb));
                var c = new Point(cx + (2 * l + 1) * HW(nb), cy + HH(nb));
                var e = new Point(cx + (2 * l - 1) * HW(nb), cy + HH(nb));
                Quad(dc, a, b, c, e, Sprites.Shade(floor, Math.Min(1, 1.2 * shade * jitter)));
                if (k > 0) Outline(dc, a, b, c, e, Sprites.Shade(floor, 0.55 * shade));
                FloorFeature(dc, lv.At(mx, my), a, b, c, e, shade, h);
                var ca = new Point(a.X, cy - HH(fb));
                var cb = new Point(b.X, cy - HH(fb));
                var cc = new Point(c.X, cy - HH(nb));
                var ce = new Point(e.X, cy - HH(nb));
                Quad(dc, ca, cb, cc, ce, Sprites.Shade(Mix(ceiling, wallCol, 0.25), Math.Min(1, 1.1 * shade * jitter)));
                if (k > 0) Outline(dc, ca, cb, cc, ce, Sprites.Shade(ceiling, 0.6 * shade));
            }
        }

        for (int k = depth; k >= 0; k--)
        {
            double nearB = k == 0 ? -0.55 : k - 1, farB = k, shade = Shade(k);
            for (int pass = 0; pass < 2; pass++)
                foreach (int l in laterals)
                {
                    if (k == 0 && l == 0) continue;
                    int mx = w.X + k * f.X + l * rt.X, my = w.Y + k * f.Y + l * rt.Y;
                    var cell = lv.At(mx, my);
                    if (!Solid(w, mx, my)) continue;
                    uint baseCol = cell == Cell.Alt ? altCol : wallCol;
                    if (pass == 0 && l != 0)
                    {
                        double edge = l < 0 ? 2 * l + 1 : 2 * l - 1;
                        double x1 = cx + edge * HW(nearB), x2 = cx + edge * HW(farB);
                        var near = (Top: new Point(x1, cy - HH(nearB)), Bottom: new Point(x1, cy + HH(nearB)));
                        var far = (Top: new Point(x2, cy - HH(farB)), Bottom: new Point(x2, cy + HH(farB)));
                        var (tl, tr, br, bl) = l < 0 ? (near.Top, far.Top, far.Bottom, near.Bottom) : (far.Top, near.Top, near.Bottom, far.Bottom);
                        DrawFace(dc, tl, tr, br, bl, baseCol, shade * 0.74, cell, mx, my, k, front: false);
                    }
                    if (pass == 1 && k >= 1)
                    {
                        double x1 = cx + (2 * l - 1) * HW(nearB), x2 = cx + (2 * l + 1) * HW(nearB);
                        double top = cy - HH(nearB), bot = cy + HH(nearB);
                        DrawFace(dc, new Point(x1, top), new Point(x2, top), new Point(x2, bot), new Point(x1, bot), baseCol, shade, cell, mx, my, k, front: true);
                    }
                }
            // What's in the squares at this depth, outermost first.
            foreach (int l in laterals)
            {
                int mx = w.X + k * f.X + l * rt.X, my = w.Y + k * f.Y + l * rt.Y;
                if (!Walkable(w, mx, my)) continue;
                DrawContents(dc, w, mx, my, k, l, cx, cy, shade);
            }
            DrawFlying(dc, w, k, f, rt, cx, cy, shade);
        }
        if (g.Hurt > 0) dc.FillRectangle(Brush((uint)(g.Hurt * 400) << 24 | 0x00C00000), v);
        if (g.Bumped > 0) dc.FillRectangle(Brush(0x40FF2020), v);
    }

    private static bool Solid(WorldState w, int x, int y)
    {
        var c = w.Here.At(x, y);
        if (Level.IsWall(c) || c == Cell.Illusion) return true;
        return Level.IsDoor(c) && !w.IsOpenDoor(x, y);
    }

    private static bool Walkable(WorldState w, int x, int y) => !Solid(w, x, y);

    private static uint Hash(int level, int x, int y)
    {
        uint h = 2166136261u ^ (uint)(level * 97);
        h ^= (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    /// <summary>Pits, plates, stairs and teleporters, drawn into the floor square.</summary>
    private void FloorFeature(DrawingContext dc, Cell cell, Point a, Point b, Point c, Point e, double shade, uint h)
    {
        Point F(double u, double t) => Lerp(Lerp(a, b, u), Lerp(e, c, u), t);
        switch (cell)
        {
            case Cell.Pit:
                Quad(dc, F(0.15, 0.15), F(0.85, 0.15), F(0.85, 0.85), F(0.15, 0.85), 0xFF000000);
                Outline(dc, F(0.15, 0.15), F(0.85, 0.15), F(0.85, 0.85), F(0.15, 0.85), Sprites.Shade(0xFF6A6A6A, shade));
                break;
            case Cell.Plate:
                Quad(dc, F(0.25, 0.25), F(0.75, 0.25), F(0.75, 0.75), F(0.25, 0.75), Sprites.Shade(0xFF7A7468, shade));
                Outline(dc, F(0.25, 0.25), F(0.75, 0.25), F(0.75, 0.75), F(0.25, 0.75), Sprites.Shade(0xFF2A2620, shade));
                break;
            case Cell.StairsDown or Cell.StairsUp:
                for (int i = 0; i < 5; i++)
                {
                    double t0 = 0.1 + i * 0.16, t1 = t0 + 0.14;
                    double tone = cell == Cell.StairsDown ? 0.9 - i * 0.16 : 0.4 + i * 0.12;
                    Quad(dc, F(0.12, t0), F(0.88, t0), F(0.88, t1), F(0.12, t1), Sprites.Shade(0xFF9A9080, shade * tone));
                }
                break;
            case Cell.Teleporter:
            {
                double flick = 0.6 + 0.4 * Math.Sin(_clock * 6 + h);
                Quad(dc, F(0.1, 0.1), F(0.9, 0.1), F(0.9, 0.9), F(0.1, 0.9), (uint)(flick * 120) << 24 | 0x0060A0FF);
                break;
            }
        }
    }

    /// <summary>What lies, stands or hangs in a square: things on the floor, open doorways, a creature, a cloud.</summary>
    private void DrawContents(DrawingContext dc, WorldState w, int mx, int my, int k, int l, double cx, double cy, double shade)
    {
        double b = Math.Max(-0.2, k - 0.5);
        double sx = cx + l * 2 * HW(b), ground = cy + HH(b), size = HW(b) / HW(0);
        var lv = w.Here;
        // An open door stands in its frame; a raised gate hangs at the top.
        if (Level.IsDoor(lv.At(mx, my)) && k >= 1)
        {
            double half = HW(b) * 0.95, top = cy - HH(b);
            uint frame = Sprites.Shade(0xFF4A3A2A, shade);
            dc.FillRectangle(Brush(frame), new Rect(sx - half, top, half * 0.14, HH(b) * 2));
            dc.FillRectangle(Brush(frame), new Rect(sx + half * 0.86, top, half * 0.14, HH(b) * 2));
            dc.FillRectangle(Brush(frame), new Rect(sx - half, top, half * 2, HH(b) * 0.12));
            if (lv.At(mx, my) == Cell.Gate)
                for (int i = 0; i < 7; i++)
                    dc.FillRectangle(Brush(Sprites.Shade(0xFF505058, shade)), new Rect(sx - half + half * 2 * i / 7 + half * 0.1, top, half * 0.06, HH(b) * 0.2));
        }
        foreach (var t in w.State.ThingsAt(mx, my).Take(4).Select((t, i) => (t, i)))
        {
            double ox = (t.i % 2 == 0 ? -0.35 : 0.35) * HW(b), oy = (t.i / 2) * 0.1 * HH(b);
            DrawIcon(dc, t.t.Item, new Point(sx + ox, ground - 14 * size - oy), Math.Max(6, 26 * size), shade);
        }
        if (k >= 1 && Engine.CreatureAt(mx, my) is { } cr)
        {
            var info = cr.Info;
            double scale = 12 * size * info.Size;
            uint tint = cr.Flash > 0 ? 0xFFFFFFFF : info.Colour;
            double alpha = info.Spirit ? 0.75 : 1;
            Sprite(dc, info.Sprite, Sprites.Shade(tint, Math.Max(0.45, shade * 1.3)), sx - 8 * scale, ground - 16 * scale - (info.Sprite is "bat" or "wisp" ? 30 * size : 0), scale, alpha);
            if (cr.Bound > 0) dc.DrawEllipse(null, Pen(0xC0FFE070, Math.Max(2, 4 * size)), new Point(sx, ground - 8 * scale), 9 * scale, 9 * scale);
            if (k <= 2)
            {
                double frac = Math.Clamp((double)cr.Health / info.Health, 0, 1);
                var bar = new Rect(sx - 40 * size, ground - 16 * scale - 18 * size, 80 * size, Math.Max(3, 6 * size));
                dc.FillRectangle(Brush(0xC0000000), bar);
                dc.FillRectangle(Brush(frac > 0.5 ? 0xFF60D060 : frac > 0.25 ? 0xFFE0C040 : 0xFFE04040), new Rect(bar.X, bar.Y, bar.Width * frac, bar.Height));
            }
        }
        foreach (var cl in Engine.Clouds.Where(c => c.X == mx && c.Y == my))
            dc.DrawEllipse(Brush(0x7060C040), null, new Point(sx, ground - HH(b) * 0.6), HW(b) * 0.9, HH(b) * 0.6);
        foreach (var bl in Engine.Blasts.Where(x => x.X == mx && x.Y == my))
        {
            double r = Math.Min(140, HW(Math.Max(0.4, b)) * (0.3 + bl.Age * 1.4));
            uint a = (uint)(Math.Clamp(1 - bl.Age * 2, 0, 1) * 220) << 24;
            dc.DrawEllipse(Brush(a | (bl.Colour & 0x00FFFFFF)), null, new Point(sx, cy), r, r);
        }
    }

    /// <summary>Fireballs and thrown things in flight, drawn between squares as they go.</summary>
    private void DrawFlying(DrawingContext dc, WorldState w, int k, (int X, int Y) f, (int X, int Y) rt, double cx, double cy, double shade)
    {
        foreach (var p in Engine.Projectiles)
        {
            var (dx, dy) = p.Dir.Step();
            double rx = p.X + dx * p.Progress - w.X, ry = p.Y + dy * p.Progress - w.Y;
            double kk = rx * f.X + ry * f.Y, ll = rx * rt.X + ry * rt.Y;
            if ((int)Math.Round(kk) != k || kk < 0.2) continue;
            double b = Math.Max(0.05, kk - 0.5);
            var at = new Point(cx + ll * 2 * HW(b), cy + HH(b) * 0.15);
            double r = 30 * HW(b) / HW(0);
            if (p.Thing is { } t) DrawIcon(dc, t.Item, at, Math.Max(6, r), Math.Max(0.4, shade));
            else
            {
                uint col = p.Kind switch { SpellKind.Lightning => 0xFFA0E0FF, SpellKind.Unmaking => 0xFFC080FF, _ => 0xFFFF8030 };
                dc.DrawEllipse(Brush(0x60000000 | (col & 0x00FFFFFF)), null, at, r * 1.6, r * 1.6);
                dc.DrawEllipse(Brush(col), null, at, r, r);
                dc.DrawEllipse(Brush(0xFFFFF8D0), null, at, r * 0.45, r * 0.45);
            }
        }
    }

    /// <summary>A thing, as a small framed glyph in its own colour.</summary>
    private static void DrawIcon(DrawingContext dc, Item item, Point at, double size, double shade = 1)
    {
        var r = new Rect(at.X - size / 2, at.Y - size / 2, size, size);
        dc.FillRectangle(Brush(Sprites.Shade(0xFF14121A, 1)), r);
        dc.DrawRectangle(Pen(Sprites.Shade(item.Colour, shade), Math.Max(1, size / 14)), r);
        Text(dc, item.Icon.ToString(), at.X, at.Y - size * 0.62, size * 1.1, Sprites.Shade(item.Colour, Math.Max(0.4, shade)), 1);
    }

    /// <summary>One wall face, given its four corners on screen (left to right); details placed in its own u/t coordinates.</summary>
    private void DrawFace(DrawingContext dc, Point tl, Point tr, Point br, Point bl, uint baseCol, double shade, Cell cell, int mx, int my, int k, bool front)
    {
        var lv = Engine.Here;
        uint h = Hash(lv.Number, mx, my);
        Point F(double u, double t) => Lerp(Lerp(tl, tr, u), Lerp(bl, br, u), t);
        void Q(double u0, double t0, double u1, double t1, uint col) => Quad(dc, F(u0, t0), F(u1, t0), F(u1, t1), F(u0, t1), col);
        void L(double u0, double t0, double u1, double t1, uint col, double width = 1) => dc.DrawLine(Pen(col, Math.Max(1, width * shade)), F(u0, t0), F(u1, t1));
        uint S(uint col, double f = 1) => Sprites.Shade(col, Math.Min(1.4, shade * f));
        uint col = Sprites.Shade(baseCol, 0.92 + (h % 16) / 100.0);
        Q(0, 0, 1, 1, S(col));

        if (cell == Cell.Alt) DrawAlt(Q, L, S, col, h, lv.AltStyle);
        else
        {
            uint line = S(col, 0.6);
            if (h % 2 == 0)
            {
                for (int i = 1; i < 6; i++) L(0, i / 6.0, 1, i / 6.0, line);
                for (int i = 0; i < 6; i++)
                    for (int j = 0; j < 4; j++)
                    {
                        double u = (j + (i % 2 == 0 ? 0.25 : 0.75)) / 4;
                        L(u, i / 6.0, u, (i + 1) / 6.0, line);
                    }
            }
            else
            {
                for (int i = 1; i < 3; i++) L(0, i / 3.0, 1, i / 3.0, line, 2);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        double u = (j + (i % 2 == 0 ? 0.5 : 0.0)) / 2 + (i % 2 == 0 ? 0 : 0.25);
                        L(u, i / 3.0, u, (i + 1) / 3.0, line, 2);
                    }
            }
        }

        switch (cell)
        {
            case Cell.Door or Cell.Locked or Cell.RuneDoor:
                Q(0.18, 0.1, 0.82, 1, S(0xFF2A1A10));
                Q(0.21, 0.13, 0.79, 1, S(0xFF6A4424));
                for (int i = 1; i < 5; i++) L(0.21 + 0.58 * i / 5, 0.14, 0.21 + 0.58 * i / 5, 1, S(0xFF3A2414), 1);
                if (cell == Cell.Locked)
                {
                    Q(0.21, 0.4, 0.79, 0.44, S(0xFF505058));
                    Q(0.21, 0.72, 0.79, 0.76, S(0xFF505058));
                    Q(0.64, 0.54, 0.7, 0.62, S(0xFFD0A040));
                }
                else if (cell == Cell.RuneDoor)
                {
                    double glow = 0.7 + 0.3 * Math.Sin(_clock * 3 + h);
                    uint rune = S(0xFF60B0FF, glow * 1.4);
                    L(0.35, 0.3, 0.45, 0.5, rune, 3); L(0.45, 0.5, 0.35, 0.7, rune, 3); L(0.55, 0.3, 0.65, 0.3, rune, 3);
                    L(0.6, 0.3, 0.6, 0.7, rune, 3); L(0.5, 0.8, 0.7, 0.8, rune, 3);
                }
                else Q(0.66, 0.55, 0.7, 0.6, S(0xFFD0B060));
                break;
            case Cell.Gate:
                Q(0.1, 0.05, 0.9, 1, S(0xFF0A0A0C));
                for (int i = 0; i <= 6; i++) Q(0.1 + 0.8 * i / 6 - 0.02, 0.05, 0.1 + 0.8 * i / 6 + 0.02, 1, S(0xFF6A6A72));
                for (int i = 1; i <= 3; i++) Q(0.1, 0.05 + i * 0.22, 0.9, 0.08 + i * 0.22, S(0xFF6A6A72));
                break;
            case Cell.Button:
                Q(0.44, 0.44, 0.56, 0.56, S(0xFF3A3A3A));
                Q(0.46, 0.46, 0.54, 0.54, S(0xFFC0A040));
                break;
            case Cell.Fountain:
                Q(0.3, 0.2, 0.7, 0.32, S(0xFF8A8A90));
                Q(0.47, 0.32, 0.53, 0.62, S(0xFF60A0E0, 1.2));
                Q(0.2, 0.62, 0.8, 0.82, S(0xFF8A8A90));
                Q(0.24, 0.62, 0.76, 0.7, S(0xFF4080C0, 1.2));
                break;
            case Cell.Writing:
                for (int i = 0; i < 5; i++)
                {
                    double t = 0.25 + i * 0.1;
                    for (int j = 0; j < 6; j++)
                    {
                        uint hh = h >> ((i * 6 + j) % 24);
                        double u0 = 0.18 + j * 0.11, u1 = u0 + 0.04 + (hh % 5) * 0.012;
                        L(u0, t, u1, t + 0.03 * ((hh >> 3) % 2), S(0xFF201C18), 2);
                    }
                }
                break;
            case Cell.Altar:
                Q(0.2, 0.6, 0.8, 1, S(0xFF9A9488));
                Q(0.15, 0.55, 0.85, 0.62, S(0xFFB0AAA0));
                Q(0.47, 0.42, 0.53, 0.55, S(0xFFE8E0C8));
                var flame = F(0.5, 0.38);
                double fl = 0.8 + 0.2 * Math.Sin(_clock * 9);
                double fr = Math.Abs(F(1, 0.4).X - F(0, 0.4).X) * 0.03 + 1;
                dc.DrawEllipse(Brush(S(0xFFFFC040, fl)), null, flame, fr, fr * 2 * fl);
                break;
            case Cell.Mirror:
            {
                Q(0.18, 0.06, 0.82, 0.94, S(0xFFB8963C));
                Q(0.23, 0.11, 0.77, 0.89, S(0xFF8090A8, 1.1));
                int index = Engine.MirrorIndex(mx, my);
                if (front && index >= 0 && !Engine.World.Freed.Contains(index))
                {
                    var c = Roster.Make()[index];
                    var tlp = F(0.3, 0.2);
                    double sc = Math.Abs(F(0.7, 0.5).X - F(0.3, 0.5).X) / 16;
                    Sprite(dc, Portrait(c), Sprites.Shade(c.Colour, Math.Min(1, shade * 1.2)), tlp.X, tlp.Y, sc, 0.85);
                }
                break;
            }
            default:
                if (cell is Cell.Wall or Cell.Illusion && k <= 3)
                {
                    if ((h >> 8) % 6 == 0) DrawTorch(dc, F, S, k);
                    else if ((h >> 8) % 9 == 1)
                    {
                        uint[] banners = [0xFF8A2020, 0xFF20406A, 0xFF2A5A2A, 0xFF6A4A10];
                        uint cloth = S(banners[(h >> 12) % 4]);
                        L(0.34, 0.08, 0.66, 0.08, S(0xFF3A2A1A), 3);
                        Quad(dc, F(0.38, 0.08), F(0.62, 0.08), F(0.62, 0.55), F(0.5, 0.48), cloth);
                        Quad(dc, F(0.38, 0.08), F(0.5, 0.48), F(0.38, 0.55), F(0.38, 0.55), cloth);
                    }
                }
                if ((h >> 14) % 5 == 0)
                {
                    uint crack = S(col, 0.35);
                    double u = 0.2 + ((h >> 17) % 6) / 10.0;
                    L(u, 0.05, u + 0.05, 0.2, crack, 2);
                    L(u + 0.05, 0.2, u - 0.02, 0.32, crack, 2);
                }
                if ((h >> 20) % 4 == 0)
                    for (int i = 0; i < 5; i++)
                    {
                        double u = ((h >> (i * 2)) % 9) / 10.0;
                        Q(u, 0.84 + (i % 2) * 0.05, u + 0.12, 1, S(0xFF2A4A22));
                    }
                break;
        }
        Outline(dc, tl, tr, br, bl, S(col, 0.35));
    }

    /// <summary>The picture of a champion: drawn by the skill they're best at.</summary>
    private static string Portrait(Champion c)
    {
        int best = Array.IndexOf(c.Xp, c.Xp.Max());
        return best switch { 0 => "warrior", 1 => "rogue", 2 => "priest", _ => "mage" };
    }

    private static void DrawAlt(Action<double, double, double, double, uint> Q, Action<double, double, double, double, uint, double> L, Func<uint, double, uint> S, uint col, uint h, string style)
    {
        switch (style)
        {
            case "hedge":
                for (int i = 0; i < 26; i++)
                {
                    double u = ((h >> (i % 24)) * 7 + i * 37) % 100 / 100.0, t = (i * 53 + (h >> 3)) % 100 / 100.0;
                    Q(u, t, Math.Min(1, u + 0.1), Math.Min(1, t + 0.08), S(col, i % 2 == 0 ? 1.25 : 0.7));
                }
                Q(0, 0, 1, 0.04, S(col, 0.6));
                break;
            case "shelves":
            {
                uint wood = S(0xFF5A3A1A, 1);
                Q(0, 0, 1, 1, S(0xFF1A120A, 1));
                for (int s = 0; s < 4; s++)
                {
                    double t0 = 0.05 + s * 0.235, t1 = t0 + 0.2;
                    uint[] spines = [0xFF8A2A2A, 0xFF2A4A7A, 0xFF4A6A2A, 0xFF7A6A3A, 0xFF5A2A5A, 0xFF9A7A4A];
                    double u = 0.04;
                    int n = 0;
                    while (u < 0.94)
                    {
                        double width = 0.035 + ((h >> ((n + s) % 20)) % 4) * 0.012;
                        double lean = ((h >> ((n * 3 + s) % 24)) % 5) * 0.012;
                        Q(u, t0 + lean, Math.Min(0.96, u + width), t1, S(spines[(n + s + (int)(h % 6)) % 6], 1));
                        u += width + 0.006;
                        n++;
                    }
                    Q(0, t1, 1, t1 + 0.03, wood);
                }
                Q(0, 0, 0.04, 1, wood);
                Q(0.96, 0, 1, 1, wood);
                break;
            }
            case "grate":
                Q(0, 0, 1, 1, S(0xFF101410, 1));
                for (int i = 0; i <= 8; i++) Q(i / 8.0 - 0.015, 0, i / 8.0 + 0.015, 1, S(0xFF5A605A, 1));
                Q(0, 0.3, 1, 0.34, S(0xFF5A605A, 1));
                Q(0, 0.7, 1, 0.74, S(0xFF5A605A, 1));
                Q(0, 0.88, 1, 1, S(0xFF2A4A3A, 1));
                break;
            case "pillars":
                Q(0, 0, 1, 1, S(col, 0.55));
                foreach (double u in new[] { 0.12, 0.62 })
                {
                    Q(u, 0.08, u + 0.26, 0.92, S(col, 1.15));
                    for (int i = 1; i < 4; i++) L(u + i * 0.065, 0.1, u + i * 0.065, 0.9, S(col, 0.7), 1);
                    Q(u - 0.04, 0.02, u + 0.30, 0.08, S(col, 1.3));
                    Q(u - 0.04, 0.92, u + 0.30, 0.98, S(col, 1.3));
                }
                break;
            case "rock":
                for (int i = 0; i < 7; i++)
                {
                    double t = i / 7.0;
                    L(0, t + ((h >> i) % 3) * 0.03, 1, t + 0.08 - ((h >> (i + 3)) % 3) * 0.03, S(col, 0.55), 2);
                    Q(((h >> (i * 2)) % 7) / 10.0, t + 0.02, ((h >> (i * 2)) % 7) / 10.0 + 0.25, t + 0.07, S(col, 1.25));
                }
                break;
            default:
                // Rubble: a heap of odd stones.
                for (int r = 0; r < 5; r++)
                for (int c = 0; c < 4; c++)
                {
                    uint s = h >> ((r * 4 + c) % 26);
                    double u = c / 4.0 + (r % 2) * 0.12 + (s % 3) * 0.02, t = r / 5.0 + (s % 2) * 0.02;
                    Q(Math.Max(0, u + 0.01), t + 0.015, Math.Min(1, u + 0.23), t + 0.185, S(col, 0.8 + (s % 5) * 0.1));
                }
                break;
        }
    }

    private void DrawTorch(DrawingContext dc, Func<double, double, Point> F, Func<uint, double, uint> S, int k)
    {
        double flicker = 0.85 + 0.15 * Math.Sin(Engine.Clock * 11 + k * 1.7) * Math.Sin(Engine.Clock * 7.3);
        var at = F(0.5, 0.3);
        double size = Math.Abs(F(1, 0.3).X - F(0, 0.3).X) * 0.05 + 2;
        for (int i = 3; i >= 1; i--)
            dc.DrawEllipse(Brush((uint)(18 * flicker) << 24 | 0x00FFA040), null, at, size * 3 * i, size * 3 * i);
        dc.DrawLine(Pen(S(0xFF2A2018, 1), Math.Max(1, size * 0.6)), F(0.5, 0.42), F(0.5, 0.3));
        dc.DrawEllipse(Brush(S(0xFFFF9020, flicker * 1.1)), null, new Point(at.X, at.Y - size), size * 0.9, size * 1.6 * flicker);
        dc.DrawEllipse(Brush(S(0xFFFFE080, flicker * 1.2)), null, new Point(at.X, at.Y - size * 0.6), size * 0.45, size * 0.8);
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static void Quad(DrawingContext dc, Point a, Point b, Point c, Point d, uint colour)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(a, true);
            g.LineTo(b);
            g.LineTo(c);
            g.LineTo(d);
            g.EndFigure(true);
        }
        dc.DrawGeometry(Brush(colour), null, geo);
    }

    private static void Outline(DrawingContext dc, Point a, Point b, Point c, Point d, uint colour)
    {
        var pen = Pen(colour);
        dc.DrawLine(pen, a, b);
        dc.DrawLine(pen, b, c);
        dc.DrawLine(pen, c, d);
        dc.DrawLine(pen, d, a);
    }
}
