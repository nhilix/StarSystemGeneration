# Unity Integration Spike — setup steps

Goal: prove the Core↔Unity contract. Success = console logs the build stats and a
galaxy map appears in Play mode.

Prereq: Unity project created at `unity/` (Universal 2D template). Then:

## 1. Reference Core as a local UPM package

`src/Core` already doubles as a UPM package (`package.json` + `StarGen.Core.asmdef`
live beside the sources; dotnet build output is hidden in dot-folders Unity ignores).

Edit `unity/Packages/manifest.json` and add to `"dependencies"`:

```json
"com.stargen.core": "file:../../src/Core",
```

On refocusing the editor, Unity imports the package ("StarGen Core" appears in the
Package Manager under *In Project*) and compiles Core. Zero console errors expected —
the asmdef sets `noEngineReferences: true`, so Core cannot even accidentally touch
UnityEngine.

## 2. Drop in the spike scene

1. Copy `GalaxyMapSpike.cs` into `unity/Assets/Scripts/` (create the folder).
2. In the sample scene: right-click Hierarchy → 2D Object → Sprite (any) — or an
   empty GameObject + add a `SpriteRenderer`.
3. Add the `GalaxyMapSpike` component to it. Inspector fields: seed 42, size 4,
   layer Polity.
4. Make sure the camera sees it (default 2D camera at origin is fine; the sprite is
   ~8 world units wide — set camera Size ≈ 5).
5. Press Play.

Expected:
- Console: `StarGen Core loaded: built 16x16 cell galaxy in ~15 ms — 4 living
  polities, 69 events.` (exact numbers for seed 42 / size 4)
- A pixel map: colored kingdom blobs (hue per polity, brighter = more developed),
  white capital pixels, dark-gray wilds, black voids. Switch layer to Density and
  replay for the grayscale disc. Try size 10 for the full spiral.

## 3. Afterward

- Commit `unity/` (gitignore already covers Library/Temp/etc.).
- Pin the exact Unity version in DESIGN.md §5 (replacing "Unity 6 LTS" placeholder).
- Delete this folder once the spike lands in the project — it's staging, not a home.
