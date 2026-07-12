# UI Visual Language & Main Menu — Design

**Date:** 2026-07-12 · **Status:** validated with user (session "ui-toolkit")
**Visual companions (living artifacts):**
- UI Language Lab — https://claude.ai/code/artifact/00bcd5f0-9c52-4819-af1c-0531c7da2eb0
  (token-driven element library of the atlas surfaces; structure × palette × FX axes)
- Main menu final mock — https://claude.ai/code/artifact/7f2aab49-7856-4dd1-9a26-ef92c2a402a3

**Feeds:** the entry scene shipped in this branch, and Slice K2's UI Toolkit
chrome (lens rail, dock, timeline — `AtlasHud.cs` is marked provisional and is
replaced by UI Toolkit per the K plan). Complements
`2026-07-11-unity-atlas-design.md`, which owns layout and behavior; this spec
owns the visual language they render in.

## Decision

**Cassette-futurism structure × Ice palette, scanlines on the world only.**

- **Structure** (shape + type): chunky CRT bezel frames with header/footer
  strips, function-key menu rows, full-inversion hover (accent background,
  ground text — the block-cursor idiom), blinking block cursors, one mono
  typeface, hard corners on controls, accent bloom via `text-shadow`.
- **Palette** (color only): Ice — blue-black ground `#060A12`, panels
  `#0A1120`/`#0E1728`, lines `#1C2A40`/`#22304A`, ink `#E6EEFA`/`#9FB2CA`/
  `#5A6F8C`, accent `#86D7FF` (dim `#35507A`), warn `#FFB000`, good `#7DDBA0`,
  critical `#FF7A6B`. Amber is *semantic* (warnings, QUIT), never decoration.
- **Scanlines**: a 1×3px tiled overlay over **world surfaces only** (menu
  starfield, atlas map viewport) — never over dense panel chrome. Alternative
  worth trying in K2: apply the tile in the map's render pass so it dollies
  with the camera.

Explored and rejected/deferred (see the Lab): pure Instrument structure (kept
as a palette-compatible alternative), Neovim-terminal structure (reads as
operator tool; revisit for god-eye if cassette tires), Phosphor-green palette
as *default* (competes with data hues; survives as a preset).

## Structure × palette architecture (the load-bearing idea)

Structure and palette are **separate stylesheets**, orthogonal by
construction:

- Component USS (`*.uss`) encodes structure and consumes **only** `var()`
  tokens — no raw hex outside palette files.
- Each palette is one token block (`SSGPalette-*.uss`) wrapped by a theme file
  (`SSG-*.tss`). Swapping the theme on `PanelSettings` re-skins every screen.
- **Color theme is a user Setting**: a dropdown of preset palettes (Ice
  default; Phosphor shipped as proof; Tokyo Night, Gruvbox specced in the Lab).
  Deferred, costed honestly: *fully custom* user colors — USS variables cannot
  be written from C# at runtime, so a custom picker needs runtime theme-asset
  generation or programmatic styling; presets first.
- The map's colors are **data** (domain hues, tension, traffic) and never
  change with the theme.
- Seam kept open: structure-follows-Eye (e.g., a future controller eye could
  wear a different structure diegetically) — costs nothing now because
  structure is its own stylesheet.

## The main menu (entry scene — keeper, stub actions)

Per the final mock: CRT bezel filling the screen; header strip
`SSG/OS 0.K — SURVEY CONTROL` / `RDY ▮`; starfield behind everything with the
scanline overlay above it and all chrome above that; ice title with bloom;
`> SELECT FUNCTION ▮` prompt; five F-key rows — **F1 NEW GALAXY** with inline
seed field, F2 CONTINUE, F3 LOAD GALAXY, F4 SETTINGS, F5 QUIT (amber); footer
strip `DETERMINISTIC KERNEL // SAME SEED, SAME SKY`.

**Signature element:** the seed field drives the starfield — deterministic
star scatter re-generated per keystroke from a hash of the seed text. Same
seed, same sky: the product's promise demonstrated before the first click.

Behavior split: **C# owns state, USS owns appearance.** The controller spawns
stars (data placement), toggles cursor-blink classes on a 530ms
`schedule.Execute`, and handles row clicks. Hover inversion, transitions, and
all styling are USS. Stubs: QUIT works (`Application.Quit`); F1–F4 log until
the K foundation is wired (NEW GALAXY will hand the seed to generation; the
scene precedes the atlas scene in the flow).

## Files (this branch)

```
unity/Assets/UI/
  Themes/SSGPalette-Ice.uss     ice tokens (:root variables)
  Themes/SSGPalette-Phosphor.uss  preset proof — same token names
  Themes/SSG-Ice.tss            default theme + ice palette
  Themes/SSG-Phosphor.tss       default theme + phosphor palette
  MainMenu/MainMenu.uxml        structure tree
  MainMenu/MainMenu.uss         cassette structure, var()-only colors
  MainMenu/MainMenuController.cs  StarGen.MenuView — starfield/blink/stubs
  MainMenu/StarGen.MenuView.asmdef
  Editor/MainMenuSceneBuilder.cs  menu item builds scene + PanelSettings +
                                  scanline texture (no binaries in git)
  Editor/StarGen.MenuView.Editor.asmdef
```

## Assets pending

- **Font Asset**: one chunky mono (candidates: VT323 for display bite, IBM
  Plex Mono Bold for legibility floor). Default runtime font until acquired —
  the USS sets no `-unity-font-definition` yet.
- Scanline tile is generated by the scene builder (`Texture2D` → PNG on
  first run), not committed.

## Gates & verification

- This branch never opens the Unity Editor (slice K owns the checkout with an
  editor attached); **the Editor-side eyeball happens after merge**: run
  `SSG → UI → Create Main Menu Scene`, enter play mode, type seeds, hover
  rows, F5 quits. Until then the committed mock artifacts are the visual
  truth and the port is line-for-line against them.
- `dotnet test` untouched (no Core changes).
- This work doubles as the application test of the
  `translating-css-to-uss` skill: the mocks were authored inside the
  constraint budget and the port must be mechanical. Deviations discovered
  during the port get folded back into the skill in the same branch.
