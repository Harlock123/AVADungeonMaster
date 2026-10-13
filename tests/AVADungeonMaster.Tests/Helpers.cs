using AVADungeonMaster.Game;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Tests;

internal static class Helpers
{
    public static SaveStore TempSaves() => new(Path.Combine(Path.GetTempPath(), "avadm-tests-" + Guid.NewGuid().ToString("N")));

    /// <summary>A quick-start game: the first four champions, standing on level 1's stairs.</summary>
    public static GameEngine Quick(int seed = 1)
    {
        var g = new GameEngine(seed, TempSaves());
        g.NewGame(quick: true);
        return g;
    }

    /// <summary>Runs the dungeon's clock for a while (with no panel open).</summary>
    public static void Wait(this GameEngine g, double seconds)
    {
        for (int i = 0; i < seconds * 60; i++) g.Tick();
    }

    /// <summary>Puts the party somewhere, facing a way, with the creatures cleared out of the level.</summary>
    public static void Stand(this GameEngine g, int depth, int x, int y, Dir facing, bool keepCreatures = false)
    {
        g.Arrive(depth, x, y, facing);
        if (!keepCreatures) g.World.State.Creatures.Clear();
        g.CloseAll();
    }
}
