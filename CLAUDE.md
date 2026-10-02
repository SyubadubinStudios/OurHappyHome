# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

The game is implemented. Planning inputs: `requirements.md` (Indonesian brief, source of truth for stack and
deliverables), `Our_Happy_Home_Game_Design.md` (full design document), `rodin.mcp.json` / `.mcp.json` (MCP servers).

## Commands

```bash
dotnet build OurHappyHome.slnx
dotnet run --project src/OurHappyHome                                   # play
dotnet test                                                             # xUnit simulation tests (~6 s)
dotnet test --filter "FullyQualifiedName~FamilyStaysHealthyForAWeek"    # single test
dotnet run --project src/OurHappyHome -- --screenshots docs/images      # regenerate doc screenshots, then exits
```

## Layout

- `src/OurHappyHome.Core` — pure simulation, no UI/graphics deps. `GameState` is everything saved (JSON via
  `SaveSystem`); `GameSession` (partial files: `.Movement`, `.Tasks`, `.AI`, `.Interactions`, `.Story`, `.Player`)
  is the runtime that ticks every family member each frame. `Scenarios/` = random/emergency events + `EventDirector`
  (Cozy/Adventure scaling). All presentation needs go through `Simulation/EventBus.cs` events.
- `src/OurHappyHome` — Avalonia 12 app on ThreeNet 0.7.0 (NuGet). `Rendering/` (GameRenderer, HouseView with wall
  cutaway, TownView, CharacterView, Effects particles, procedural Textures), `Audio/` (procedural Synth, SoundBank,
  MusicComposer, AudioManager on ThreeNet's mixer), `Views/` (MainWindow, MenuScreen, GameScreen partials: HUD,
  Panels, Build; MiniGames, AboutScreen with scrolling credits, ScreenshotDirector), `UI/` (code-built UI kit).
- `tests/OurHappyHome.Tests` — headless simulation tests (multi-day runs, scenarios, save/load, navigation).
- `art/` — concept art, raw Rodin GLBs, rigged characters, optimized props, voice MP3s. Game copies live in
  `src/OurHappyHome/Assets/{Models,Voice,Art}`.
- `tools/blender/` — `rig_character.py` (auto-rig + 12 animations + GLB export; run through Blender MCP with
  `exec(open(path).read(), ns); ns["process"](src, dst, height)`), `optimize_prop.py`, preview scripts.
- `docs/` — bilingual-ish documentation (Indonesian) with screenshots; `README.md` at root.

## Gotchas

- Never call `bpy.ops.wm.read_factory_settings` through Blender MCP: it unloads the MCP add-on.
- Rodin MCP calls fail when issued in parallel; generate one at a time.
- ThreeNet `Node.Visible` has no getter; track visibility yourself. Clips from a GLB import are the last
  `ImportResult.AnimationCount` entries of `Scene.Animations`; animated models must be imported per instance
  (not `Node.Clone`).
- Text in `Textures.Label` must be drawn at the padding offset; centring with `MaxTextWidth` pushes it off-texture.
- Furniture approach points are computed outside the footprint (`FurnitureItem.ApproachPoint`); walks time out
  via `MemberTask.WalkBudget` so nobody gets stuck forever.
- Character rigs: models face +Z, feet at y = 0; sit/lie offsets use the hip heights in `CharacterView.Proportions`.

## Required tech stack (from requirements.md)

- **.NET 10** with **Avalonia** for the UI and app shell.
- **ThreeNet** for 3D rendering: https://github.com/DotNetVibeCoderz/Vibe_Graphics/tree/main/ThreeNet. It is a third-party library, so check its actual API in that repo instead of assuming it matches three.js or another engine.
- Include visual effects, sound effects and music.
- **Assets:** generate game art, concept art and 3D models with the Rodin MCP. Rig and animate characters with the Blender MCP.
  - Blender MCP is `blender` in `.mcp.json`, started with `uvx blender-mcp`. It talks to the BlenderMCP add-on in Blender 5.2 on port 9876, so Blender must be open with the add-on connected.
  - Use its `execute_blender_code` tool for rigging and animation, and `export_scene` to export.
  - Rodin is registered in `.mcp.json`, which is a copy of `rodin.mcp.json`. It runs a local executable at `C:\experiment\MCP\release\rodin\rodin.exe`. Its tools are `Generate3DFromPrompt`, `Generate3DFromImageUrl` and `Generate3DFromImageUrlAndPrompt`, which output .glb files, plus `GenerateImageWithNanoBanana2` and `GenerateImageWithQwenImage2` for images.

## Scope expectations

- Build the game from the design document, then extend it with whatever else makes it more complete and fun. The brief explicitly asks for this, so going beyond the design document is expected.
- The **About** menu must credit "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios" and include **scrolling credits**.

## Design constraints to keep in mind

These come from the design document and affect architecture across systems:

- **Five independently simulated family members:** Father (25), Mother (24), Older Sister (11), the player character (boy, 10) and Younger Sister (8). Each has their own needs, mood, schedule, skills, memories and pairwise relationships. They act on their own rather than waiting for player commands, so the simulation must tick every character and not only the controlled one.
- **Systems are meant to interact and produce emergent stories.** For example, weather can cause a power outage, which raises fear, which drives a family response, which becomes a memory. Prefer shared simulation state plus an event bus over isolated, scripted features.
- **Family-friendly by design:** no combat focus and no graphic injury or death. Danger leads to Down, Trapped or Needs Help states and rescue. The failure rule is "Nobody Gets Left Behind," and a failed scenario offers a restart.
- **Cozy Mode and Adventure Mode** scale how often and how intensely dangerous events happen. Event generation should be parameterized by these modes.
- **Stamina thresholds:** 100% Ready, 60% Tired, 20% Exhausted, 0% Must recover.
- **Cooking quality tiers:** Failed, Poor, Normal, Delicious, Perfect.
- **The UI must be usable by young players.** It should show the current character, needs, stamina, mood, objective, time and date, weather, money, and a family status panel. The game must also support pause and the accessibility options listed in section 27 of the design document.
