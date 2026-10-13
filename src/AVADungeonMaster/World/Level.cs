namespace AVADungeonMaster.World;

public enum Dir { North, East, South, West }

public static class Dirs
{
    public static (int X, int Y) Step(this Dir d) => d switch
    {
        Dir.North => (0, -1), Dir.East => (1, 0), Dir.South => (0, 1), _ => (-1, 0),
    };

    public static Dir Left(this Dir d) => (Dir)(((int)d + 3) % 4);
    public static Dir Right(this Dir d) => (Dir)(((int)d + 1) % 4);
    public static Dir Back(this Dir d) => (Dir)(((int)d + 2) % 4);
}

/// <summary>What a square of a level is.</summary>
public enum Cell
{
    Floor, Wall, Alt, Door, Gate, Locked, RuneDoor, Illusion, StairsUp, StairsDown, Pit, Teleporter, Plate,
    Button, Fountain, Writing, Altar, Mirror,
}

/// <summary>
/// One level of the Deepvault, as designed: a 16×16 map ('#' wall, '%' second wall, '.' floor, 'D'
/// door, 'G' gate worked by a plate or button, 'L' locked door, 'R' rune-sealed door, 'I' a wall you
/// can walk through, '&lt;' '&gt;' stairs, 'O' pit, 'T' teleporter, '^' pressure plate, and in the walls
/// 'B' button, 'F' fountain, 'W' writing, 'A' altar, 'M' mirror), and what starts where.
/// </summary>
public sealed class Level
{
    public const int Size = 16;

    public required int Number { get; init; }
    public required string Name { get; init; }
    public required string[] Map { get; init; }
    public string WallColour { get; init; } = "#6A6458";
    public string AltColour { get; init; } = "#4A4438";
    public string FloorColour { get; init; } = "#2A2622";
    public string CeilingColour { get; init; } = "#141210";
    public string AltStyle { get; init; } = "rubble";
    /// <summary>Where the party first stands (the Hall), otherwise on the up stairs.</summary>
    public (int X, int Y)? Start { get; init; }
    public (int X, int Y, string Id)[] Items { get; init; } = [];
    public (int X, int Y, string Kind)[] Monsters { get; init; } = [];
    /// <summary>Locked doors, and the key each one takes.</summary>
    public Dictionary<(int, int), string> Locks { get; init; } = [];
    /// <summary>Plates and buttons, and the gates they open.</summary>
    public Dictionary<(int, int), (int X, int Y)[]> Triggers { get; init; } = [];
    public Dictionary<(int, int), (int X, int Y)> Teleports { get; init; } = [];
    public Dictionary<(int, int), string> Writing { get; init; } = [];

    public Cell At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Size || y >= Size) return Cell.Wall;
        return Map[y][x] switch
        {
            '#' => Cell.Wall, '%' => Cell.Alt, 'D' => Cell.Door, 'G' => Cell.Gate, 'L' => Cell.Locked, 'R' => Cell.RuneDoor,
            'I' => Cell.Illusion, '<' => Cell.StairsUp, '>' => Cell.StairsDown, 'O' => Cell.Pit, 'T' => Cell.Teleporter,
            '^' => Cell.Plate, 'B' => Cell.Button, 'F' => Cell.Fountain, 'W' => Cell.Writing, 'A' => Cell.Altar, 'M' => Cell.Mirror,
            _ => Cell.Floor,
        };
    }

    /// <summary>Wall of one kind or another (things to look at, not walk into).</summary>
    public static bool IsWall(Cell c) => c is Cell.Wall or Cell.Alt or Cell.Button or Cell.Fountain or Cell.Writing or Cell.Altar or Cell.Mirror;

    public static bool IsDoor(Cell c) => c is Cell.Door or Cell.Gate or Cell.Locked or Cell.RuneDoor;

    public (int X, int Y) Find(char ch)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                if (Map[y][x] == ch) return (x, y);
        return (-1, -1);
    }
}

public static partial class Levels
{
    public static Level Get(int n) => All[n];
    public const int Deepest = 8;
}
