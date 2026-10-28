# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers the story, the controls, what the tests
check and downloads; this file covers how the code is put together and the rules that keep it working.

## What this is

A real-time, first-person dungeon crawler in the style of FTL's 1987 Dungeon Master, in C# on .NET 10 and Avalonia
12 (12.1.4). The dungeon, champions, creatures, rune language and maps are all original. Everything is drawn in
code; the sound effects and tunes are synthesised by `tools/make_sounds.py`. One self-contained executable per
platform (Windows, macOS and Linux, x64 and ARM).

## Commands

```bash
dotnet build AVADungeonMaster.slnx                  # the SDK version is in global.json (10.0.400 or later)
dotnet test AVADungeonMaster.slnx                   # every test runs headless: no window opens
dotnet run --project src/AVADungeonMaster           # play it; --no-audio for silence
DUNGEON_SHOTS=$PWD/Images dotnet test tests/AVADungeonMaster.Tests --filter ScreenshotTests   # refresh the README's pictures
python3 tools/make_sounds.py                        # rebuild src/AVADungeonMaster/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types, implicit usings and invariant
globalization for every project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVADungeonMaster/Game/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` holds the fixed 60 Hz clock (`Update` and `Tick`), the panel stack and the key entry point
    `Press(name)`.
  - Partial files: `.Party` (walking, using things, stairs, pits, light, hunger, fighting), `.Creatures` (hunting,
    wandering, striking), `.Magic` (runes, spells, projectiles, clouds) and `.Menus` (title, mirrors, packs,
    saving, help, defeat and victory).
  - `Panel.cs` is a menu or message on the stack. `DemoPilot.cs` plays the attract demo. `SoundManager.cs` and
    `Sfx.cs` play ambience and effects.
- `src/AVADungeonMaster/World/`
  - `Level.cs`: the `Cell` types, `Dir` and the map legend.
  - `Levels.cs`: the nine levels (the Hall of Mirrors and eight below), each a hand-drawn 16x16 text map with its
    items, creatures, locks, switches and writing.
  - `Roster.cs` (the twelve champions), `WorldState.cs` (everything a save holds) and `SaveStore.cs`.
- `src/AVADungeonMaster/Rules/`: champions and skills, items and weapon actions, runes and spells, creatures, and
  `Rng`.
- `src/AVADungeonMaster/Views/`: `GameCanvas` (split over partial files) maps keys to names, runs the 16 ms timer
  and draws the first-person view (`GameCanvas.View3D.cs`) and the panels. `Sprites.cs` holds the pixel art.
- `src/AVADungeonMaster/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVADungeonMaster.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `WorldTests` (with the level solver),
  `GameplayTests` and `ScreenshotTests`; `Helpers.cs` sets up games.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` feeds real time into an accumulator and advances in fixed 1/60 s `Tick`s,
  independent of the frame rate, so tests run the dungeon deterministically from a seed. While a panel is open the
  dungeon waits.
- **One random source.** All chance comes from the engine's `Rng` (xorshift). Its state is saved as
  `WorldState.Dice`, so a loaded game carries on where it left off. Never use `Random` in game logic.
- **Keys go in by name.** `GameCanvas.KeyName` turns Avalonia keys into names ("Up", "Z", "Enter"...), and the
  engine takes only those names through `Press`. Tests and the attract demo play the same way, so test new
  controls through `Press`. "Up" walks the way the party faces; north is `dy = -1`.
- **Levels must stay solvable.** `WorldTests` checks every level is closed and complete, that every lock has its
  key and every gate its plate or button, and runs a solver that proves each level can be finished. Edit a map in
  `Levels.cs` using the legend in `Level.cs`, then run the tests.
- **Engine first, drawing second.** Gameplay belongs in `Game/`, `World/` and `Rules/`; `Views/` only reads the
  engine and draws it.
- **Audio.** OpenAL Soft through Silk.NET; `AudioDecoders` reads WAV, Ogg (NVorbis) and MP3 (NLayer).
  `Directory.Build.targets` swaps in the official Windows x64/x86 `soft_oal.dll` from `Audio/native/`, which needs
  no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger mixing period to `ALSOFT_CONF`.
- **Tests never touch the player's files.** Saves live in the roaming app-data folder (`AVADungeonMaster`). Tests
  use a fresh temporary folder (`Helpers.TempSaves()`) and a silent `SoundManager`. The attract demo must never
  touch saved games. In-game screenshots (Ctrl+S) go to the Pictures folder.
- **Line endings.** The sources use LF.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request with .NET 10. It then publishes
self-contained single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64, kept as workflow
artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper
release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
