using AVADungeonMaster.Game;
using AVADungeonMaster.Rules;
using AVADungeonMaster.World;

namespace AVADungeonMaster.Tests;

public class GameplayTests
{
    [Fact]
    public void The_hall_lets_you_choose_champions_from_the_mirrors()
    {
        var g = new GameEngine(1, Helpers.TempSaves());
        g.Press("Enter");                       // Enter the Hall of Mirrors
        Assert.Equal(Mode.Play, g.Mode);
        Assert.Empty(g.World.Party);
        // The stairs refuse a party of none.
        g.Stand(0, 7, 2, Dir.North);
        g.Press("Up");
        Assert.Equal(0, g.World.Depth);
        // Face the first mirror on the left and free its champion.
        g.Stand(0, 6, 4, Dir.West);
        g.Press("Space");
        Assert.Equal(PanelKind.Mirror, g.Top!.Kind);
        g.Press("1");
        Assert.Single(g.World.Party);
        Assert.Equal("Brynja Ironhand", g.World.Party[0].Name);
        g.Press("Space");
        Assert.Single(g.World.Party);           // the mirror is empty now
        g.Stand(0, 7, 2, Dir.North);
        g.Wait(0.3);
        g.Press("Up");
        Assert.Equal(1, g.World.Depth);
    }

    [Fact]
    public void Quick_start_puts_four_champions_on_the_first_level()
    {
        var g = Helpers.Quick();
        Assert.Equal(4, g.World.Party.Count);
        Assert.Equal(1, g.World.Depth);
        Assert.Equal(Levels.Get(1).Find('<'), (g.World.X, g.World.Y));
        Assert.Equal([0, 1, 2, 3], g.World.Party.Select(c => c.Slot));
        Assert.True(g.World.Walkable(g.Ahead.X, g.Ahead.Y));
    }

    [Fact]
    public void Walls_stop_the_party_and_doors_open_with_a_hand()
    {
        var g = Helpers.Quick();
        g.Stand(1, 3, 2, Dir.East);             // facing the door at (4,2)
        g.Press("Up");
        Assert.Equal((3, 2), (g.World.X, g.World.Y));
        g.Press("Space");
        Assert.True(g.World.IsOpenDoor(4, 2));
        g.Wait(0.3);
        g.Press("Up");
        Assert.Equal((4, 2), (g.World.X, g.World.Y));
    }

    [Fact]
    public void A_plate_raises_a_gate_and_a_button_another()
    {
        var g = Helpers.Quick();
        Assert.False(g.World.Walkable(7, 5));
        g.Stand(1, 2, 5, Dir.South);
        g.Press("Up");                          // onto the plate at (2,6)
        Assert.True(g.World.Walkable(7, 5));
        g.Stand(1, 12, 12, Dir.South);          // facing the button at (12,13)
        Assert.False(g.World.Walkable(11, 12));
        g.Press("Space");
        Assert.True(g.World.Walkable(11, 12));
    }

    [Fact]
    public void A_locked_door_needs_its_key_and_uses_it_up()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 8, Dir.South);            // facing the iron door at (6,9)
        g.Press("Space");
        Assert.False(g.World.Walkable(6, 9));
        g.World.Party[1].Pack.Add(new Thing("ironkey"));
        g.Press("Space");
        Assert.True(g.World.Walkable(6, 9));
        Assert.DoesNotContain(g.World.Party.SelectMany(c => c.Everything), t => t.Id == "ironkey");
    }

    [Fact]
    public void Things_on_the_floor_can_be_picked_up_and_put_down()
    {
        var g = Helpers.Quick();
        g.Stand(1, 9, 5, Dir.North);
        Assert.Contains(g.World.State.ThingsAt(9, 5), t => t.Id == "ironkey");
        g.Press("G");
        Assert.Empty(g.World.State.ThingsAt(9, 5));
        var holder = g.World.Party.Single(c => c.Everything.Any(t => t.Id == "ironkey"));
        g.OpenPack(holder.Slot);
        int at = g.Top!.Options.FindIndex(o => o.Label.Contains("Iron Key"));
        g.Top.Selected = at;
        g.Press("Enter");
        int drop = g.Top!.Options.FindIndex(o => o.Label.StartsWith("Put it down"));
        g.Top.Selected = drop;
        g.Press("Enter");
        Assert.Contains(g.World.State.ThingsAt(9, 5), t => t.Id == "ironkey");
    }

    [Fact]
    public void Stairs_lead_down_and_back_up()
    {
        var g = Helpers.Quick();
        var down = Levels.Get(1).Find('>');
        g.Stand(1, down.X - 1, down.Y, Dir.East);
        g.Press("Up");
        Assert.Equal(2, g.World.Depth);
        Assert.Equal(Levels.Get(2).Find('<'), (g.World.X, g.World.Y));
        g.Wait(0.3);
        g.Stand(2, 1, 2, Dir.North, keepCreatures: true);
        g.Press("Up");
        Assert.Equal(1, g.World.Depth);
        Assert.Equal(down, (g.World.X, g.World.Y));
    }

    [Fact]
    public void A_pit_drops_the_party_to_the_level_below_and_hurts()
    {
        var g = Helpers.Quick();
        g.Stand(2, 9, 1, Dir.East);             // the pit at (10,1)
        int hp = g.World.Party.Sum(c => c.Health);
        g.Press("Up");
        Assert.Equal(3, g.World.Depth);
        Assert.True(g.World.Walkable(g.World.X, g.World.Y));
        Assert.True(g.World.Party.Sum(c => c.Health) < hp);
    }

    [Fact]
    public void A_teleporter_carries_the_party_away()
    {
        var g = Helpers.Quick();
        g.Stand(3, 5, 5, Dir.East);             // the teleporter at (6,5)
        g.Press("Up");
        Assert.Equal((1, 12), (g.World.X, g.World.Y));
    }

    [Fact]
    public void A_fountain_fills_everyone_and_their_waterskins()
    {
        var g = Helpers.Quick();
        foreach (var c in g.World.Party) c.Water = 10;
        var skin = new Thing("waterskin") { Charge = 0 };
        g.World.Party[0].Pack.Add(skin);
        g.Stand(1, 2, 10, Dir.West);            // the fountain at (1,10)
        g.Press("Space");
        Assert.All(g.World.Party, c => Assert.Equal(100, c.Water));
        Assert.Equal(3, skin.Charge);
    }

    [Fact]
    public void Champions_grow_hungry_and_thirsty_and_torches_burn_out()
    {
        var g = Helpers.Quick();
        var c = g.World.Party[0];
        c.Right = new Thing("torch") { Charge = 3 };
        double light = g.Light;
        g.Wait(5);
        Assert.Null(c.Right);
        Assert.True(g.Light < light);
        Assert.True(c.Food < 100 && c.Water < 100);
        Assert.True(c.Water < c.Food);
    }

    [Fact]
    public void Eating_and_drinking_from_the_pack()
    {
        var g = Helpers.Quick();
        var c = g.World.Party[2];
        c.Food = 20;
        c.Pack.Add(new Thing("bread"));
        g.OpenPack(2);
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.Contains("Bread"));
        g.Press("Enter");
        g.Top!.Selected = g.Top.Options.FindIndex(o => o.Label.StartsWith("Eat"));
        g.Press("Enter");
        Assert.Equal(55, c.Food, 1);
        Assert.DoesNotContain(c.Pack, t => t.Id == "bread");
    }

    [Fact]
    public void A_front_champion_cuts_down_a_creature_and_learns_from_it()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 4, Dir.North);
        g.World.State.Creatures.Add(new Creature { Kind = "gnawer", X = 6, Y = 3, Health = 6, AttackTimer = 99, MoveTimer = 99 });
        var brynja = g.World.Party[0];
        int xp = brynja.Xp[(int)Skill.Fighter];
        for (int i = 0; i < 20 && g.CreatureAt(6, 3) is not null; i++)
        {
            g.Press("Z");
            g.Wait(1.2);
        }
        Assert.Null(g.CreatureAt(6, 3));
        Assert.True(brynja.Xp[(int)Skill.Fighter] > xp);
        Assert.Equal(1, g.World.Kills);
    }

    [Fact]
    public void The_back_row_cannot_reach_with_a_blade_but_can_throw()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 4, Dir.North);
        var cr = new Creature { Kind = "mudling", X = 6, Y = 2, Health = 99, AttackTimer = 99, MoveTimer = 99 };
        g.World.State.Creatures.Add(cr);
        var taric = g.World.Party[1];
        (taric.Slot, g.World.Party[3].Slot) = (3, 1);
        g.World.Party.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        g.Press("V");                           // Taric, now at the back, stabs: can't reach
        Assert.Equal(99, cr.Health);
        g.Press("F");                           // and throws his dagger instead
        g.Wait(1);
        Assert.True(cr.Health < 99);
        Assert.Contains(g.World.State.ThingsAt(6, 2), t => t.Id == "dagger");
    }

    [Fact]
    public void Spirits_shrug_off_ordinary_blades()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 4, Dir.North);
        var wisp = new Creature { Kind = "wisp", X = 6, Y = 3, Health = 34, AttackTimer = 99, MoveTimer = 99 };
        g.World.State.Creatures.Add(wisp);
        for (int i = 0; i < 8; i++) { g.Press("Z"); g.Wait(1.2); }
        Assert.Equal(34, wisp.Health);
        g.World.Party[0].Right = new Thing("moonblade");
        for (int i = 0; i < 8 && wisp.Health == 34; i++) { g.Press("Z"); g.Wait(1.2); }
        Assert.True(wisp.Health < 34);
    }

    [Fact]
    public void Creatures_close_in_and_strike_the_side_they_come_from()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 4, Dir.North);
        g.World.State.Creatures.Add(new Creature { Kind = "mudling", X = 6, Y = 8, Health = 99 });
        int front = g.World.Party.Where(c => c.Front).Sum(c => c.Health), back = g.World.Party.Where(c => !c.Front).Sum(c => c.Health);
        g.Wait(11);
        Assert.Equal(1, Math.Abs(g.World.State.Creatures[0].Y - 4) + Math.Abs(g.World.State.Creatures[0].X - 6));
        Assert.True(g.World.Party.Where(c => !c.Front).Sum(c => c.Health) < back, "it came from behind");
        Assert.True(g.World.Party.Where(c => c.Front).Sum(c => c.Health) >= front, "the front row was left alone");
    }

    [Fact]
    public void Runes_cast_spells_and_cost_mana_even_when_they_fail()
    {
        var g = Helpers.Quick();
        g.Stand(1, 6, 8, Dir.East);
        while (g.World.Party[g.Caster].Name != "Ysmar the Grey") g.Press("Tab");
        var ysmar = g.World.Party[g.Caster];
        ysmar.Mana = 30;
        ysmar.Xp[(int)Skill.Wizard] = 3000;
        g.World.State.Creatures.Add(new Creature { Kind = "gnawer", X = 9, Y = 8, Health = 14, AttackTimer = 99, MoveTimer = 99 });
        g.Press("2"); g.Press("1"); g.Press("1");  // OS FLA JAC: a fireball
        Assert.Equal(["OS", "FLA", "JAC"], g.Spoken);
        g.Press("Enter");
        Assert.Equal(30 - Runes.Cost(["OS", "FLA", "JAC"]), ysmar.Mana, 1);
        g.Wait(1.5);
        Assert.Null(g.CreatureAt(9, 8));
        Assert.Contains("Fireball", g.World.Spellbook);
        // Nonsense runes cost mana and do nothing.
        double mana = ysmar.Mana;
        g.Wait(1.2);
        g.Press("1"); g.Press("6");             // IN MOR
        g.Press("Enter");
        Assert.True(ysmar.Mana < mana);
    }

    [Fact]
    public void Light_lets_the_party_see_further()
    {
        var g = Helpers.Quick();
        foreach (var c in g.World.Party) { c.Left = c.Left?.Item.Kind == ItemKind.Torch ? null : c.Left; c.Right = c.Right?.Item.Kind == ItemKind.Torch ? null : c.Right; }
        int dark = g.ViewDepth;
        var wenna = g.World.Party.First(c => c.Name == "Old Wenna");
        while (g.World.Party[g.Caster] != wenna) g.Press("Tab");
        wenna.Xp[(int)Skill.Wizard] = 3000;
        wenna.Mana = 30;
        g.Press("4"); g.Press("5");             // EL LUM
        g.Press("Enter");
        Assert.True(g.ViewDepth > dark);
        Assert.True(g.World.MagicLight > 0);
    }

    [Fact]
    public void Mend_heals_the_most_wounded_and_unseal_opens_a_rune_door()
    {
        var g = Helpers.Quick();
        var wenna = g.World.Party.First(c => c.Name == "Old Wenna");
        while (g.World.Party[g.Caster] != wenna) g.Press("Tab");
        wenna.Xp[(int)Skill.Priest] = 6000;
        wenna.Xp[(int)Skill.Wizard] = 6000;
        wenna.Mana = 40;
        g.World.Party[0].Health = 5;
        g.Press("3"); g.Press("3"); g.Press("4"); // UR AQU SUI
        g.Press("Enter");
        Assert.True(g.World.Party[0].Health > 5);
        g.Wait(1.2);
        g.Stand(4, 3, 3, Dir.East);             // facing the rune door at (4,3)
        Assert.False(g.World.Walkable(4, 3));
        g.Press("3"); g.Press("4");             // UR TER
        g.Press("Enter");
        Assert.True(g.World.Walkable(4, 3));
    }

    [Fact]
    public void A_ward_halves_the_blows()
    {
        var g = Helpers.Quick();
        var c = g.World.Party[0];
        int hp = c.Health;
        g.World.Ward = 30;
        g.HurtChampion(c, 10);
        Assert.Equal(hp - 5, c.Health);
    }

    [Fact]
    public void A_fallen_champion_drops_everything_and_an_altar_brings_them_back()
    {
        var g = Helpers.Quick();
        g.Stand(1, 1, 13, Dir.South);           // the altar at (1,14)? -> look for it
        var altar = Levels.Get(1).Find('A');
        var c = g.World.Party[1];
        g.Stand(1, altar.X + 1, altar.Y, Dir.West);
        g.HurtChampion(c, 999);
        Assert.False(c.Alive);
        Assert.Empty(c.Everything);
        Assert.NotEmpty(g.World.State.ThingsAt(g.World.X, g.World.Y));
        g.Press("Space");
        Assert.True(c.Alive);
    }

    [Fact]
    public void When_all_four_fall_the_game_is_lost()
    {
        var g = Helpers.Quick();
        foreach (var c in g.World.Party) g.HurtChampion(c, 999);
        g.Wait(0.1);
        Assert.Equal(Mode.Dead, g.Mode);
        Assert.Equal("The party has fallen", g.Top!.Title);
    }

    [Fact]
    public void Saving_and_loading_bring_back_the_dungeon_as_it_was()
    {
        var g = Helpers.Quick();
        g.Stand(1, 3, 2, Dir.East, keepCreatures: true);
        g.Press("Space");                       // open the door
        g.World.Party[0].Pack.Add(new Thing("gem"));
        g.World.Party[2].Food = 33;
        int creatures = g.World.State.Creatures.Count;
        g.SaveTo(2);
        string before = g.World.ToJson();
        g.World.Party[2].Food = 99;
        g.World.State.Open.Clear();
        Assert.True(g.LoadFrom(2));
        Assert.True(g.World.IsOpenDoor(4, 2));
        Assert.Equal(33, g.World.Party[2].Food, 1);
        Assert.Contains(g.World.Party[0].Pack, t => t.Id == "gem");
        Assert.Equal(creatures, g.World.State.Creatures.Count);
        Assert.Equal(before.Replace(g.World.Saved.ToString("O"), ""), g.World.ToJson().Replace(g.World.Saved.ToString("O"), ""));
    }

    [Fact]
    public void Bound_with_the_starheart_vorthane_can_be_unmade()
    {
        var g = Helpers.Quick();
        g.Stand(8, 7, 9, Dir.North);
        var v = new Creature { Kind = "vorthane", X = 7, Y = 8, Health = 60, AttackTimer = 99, MoveTimer = 99 };
        g.World.State.Creatures.Add(v);
        var brynja = g.World.Party[0];
        brynja.Right = new Thing("starheart");
        brynja.Stamina = 99;
        g.Press("A");                           // BIND
        Assert.True(v.Bound > 0);
        for (int i = 0; i < 20 && g.Mode == Mode.Play; i++)
        {
            g.Wait(1.1);
            g.Press("Z");
        }
        Assert.Equal(Mode.Won, g.Mode);
        Assert.True(g.World.Won);
        Assert.Equal(PanelKind.Ending, g.Top!.Kind);
    }

    [Fact]
    public void The_title_turns_into_a_demonstration_that_plays_by_itself()
    {
        var saves = Helpers.TempSaves();
        var g = new GameEngine(5, saves);
        g.Wait(GameEngine.DemoAfter + 1);
        Assert.True(g.InDemo);
        var start = (g.World.X, g.World.Y);
        g.Wait(20);
        Assert.NotEqual(start, (g.World.X, g.World.Y));
        g.Press("Space");
        Assert.False(g.InDemo);
        Assert.Equal(Mode.Title, g.Mode);
        Assert.False(saves.Any());
    }
}
