using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVADungeonMaster.Game;
using AVADungeonMaster.Rules;
using AVADungeonMaster.Views;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set DUNGEON_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, Helpers.TempSaves());
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("DUNGEON_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVADungeonMaster-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_the_hall_of_mirrors()
    {
        var (w, c, g) = Open();
        g.Wait(1);
        Shot(w, c, "title");
        g.Press("Enter");
        g.Stand(0, 7, 8, Dir.North);
        Shot(w, c, "hall");
        g.Stand(0, 6, 6, Dir.West);
        g.Press("Space");
        Shot(w, c, "mirror");
    }

    [AvaloniaFact]
    public void Exploring_and_fighting()
    {
        var (w, c, g) = Open(12);
        g.NewGame(quick: true);
        g.World.Party[2].Left = new Thing("torch");
        g.Stand(1, 6, 8, Dir.North, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "mudling", X = 6, Y = 5, Health = 45, MoveTimer = 99, AttackTimer = 99 });
        g.Say("Somewhere nearby, stone grinds on stone.", 0xFFC0C0D0);
        g.Say("A Mudling is in the way.");
        Shot(w, c, "gatehalls");
        g.Stand(2, 4, 10, Dir.East, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "bonesentry", X = 5, Y = 10, Health = 50, MoveTimer = 99, AttackTimer = 99 });
        g.Press("Z");
        g.Wait(0.2);
        Shot(w, c, "fight");
        g.World.MagicLight = 200;
        g.World.MagicLightPower = 1.5;
        g.Stand(1, 6, 2, Dir.East, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "crawler", X = 9, Y = 2, Health = 100, MoveTimer = 99, AttackTimer = 99 });
        while (g.Caster != 3) g.Press("Tab");
        g.World.Party[3].Mana = 30;
        g.World.Party[3].Xp[(int)Skill.Wizard] = 3000;
        g.Press("3"); g.Press("1"); g.Press("1");
        Shot(w, c, "runes");
        g.Press("Enter");
        g.Wait(0.4);
        Shot(w, c, "fireball");
    }

    [AvaloniaFact]
    public void Deeper_down()
    {
        var (w, c, g) = Open(13);
        g.NewGame(quick: true);
        foreach (var ch in g.World.Party) ch.Right ??= new Thing("torch");
        g.World.MagicLight = 300;
        g.World.MagicLightPower = 1.2;
        g.Stand(4, 7, 9, Dir.North, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "wisp", X = 7, Y = 7, Health = 80, MoveTimer = 99, AttackTimer = 99 });
        Shot(w, c, "cistern");
        g.Stand(5, 9, 7, Dir.West, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "emberimp", X = 7, Y = 7, Health = 100, MoveTimer = 99, AttackTimer = 99 });
        Shot(w, c, "forge");
        g.Stand(7, 8, 13, Dir.North, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "deepwyrm", X = 8, Y = 11, Health = 600, MoveTimer = 99, AttackTimer = 99 });
        Shot(w, c, "deepwyrm");
        g.Stand(8, 7, 10, Dir.North, keepCreatures: true);
        g.World.State.Creatures.Clear();
        g.World.State.Creatures.Add(new Creature { Kind = "vorthane", X = 7, Y = 8, Health = 1400, MoveTimer = 99, AttackTimer = 99, Bound = 5 });
        Shot(w, c, "vorthane");
    }

    [AvaloniaFact]
    public void A_champion_and_their_pack()
    {
        var (w, c, g) = Open(14);
        g.NewGame(quick: true);
        g.World.Party[0].Pack.AddRange([new Thing("bread"), new Thing("healing"), new Thing("ironkey")]);
        g.World.Party[0].Xp[(int)Skill.Fighter] = 1600;
        g.OpenPack(0);
        Shot(w, c, "pack");
    }
}
