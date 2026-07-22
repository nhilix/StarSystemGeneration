# Slice AC kickoff — the atlas catch-up (the sim the atlas shows becomes the sim that runs)

You are opening **Slice AC**: the Unity atlas, complete through K5 and last
touched by Slice L's placement fix, catches up with everything the sim has
become since — the contract economy (CE), the whole currency chain (CU-1..4 +
BF), domain expansion (DX), and off-lane locality (L2). Four surface groups,
four phase gates, zero sim behavior. The design is approved and committed;
this slice implements it **directly from the spec** per the lighter protocol —
no separate writing-plans document. The committed task ledger
(`docs/superpowers/plans/2026-07-21-slice-ac-ledger.md`, which you create) is
the resumability record.

Branch `slice-ac-atlas-catchup`. Worktree `.worktrees/slice-ac-atlas-catchup/`.

**This slice supersedes the original K6** (`2026-07-12-slice-k6-kickoff-prompt.md`)
— do not run that prompt; its whole scope is Phase 2 here, and its reading
list is folded into this one.

## Read first, in order

1. **`docs/superpowers/specs/2026-07-21-ac-atlas-catchup-design.md`** — the
   authoritative design. The four phases, the per-phase eyeball decision, the
   K-slice invariants (zero sim behavior, golden byte-untouched, REPL parity
   via shared Core.Atlas queries), and the boundary all live here.
2. The K3 ledger (`2026-07-12-slice-k3-ledger.md`) — the SelectionModel /
   InspectorDock / DockKit / PanelViews / LegendQuery architecture you EXTEND,
   and the drift-proof legend pattern (`LegendDriftTests`).
3. The K5 ledger (`2026-07-12-slice-k5-ledger.md`) — SystemStage, SystemQuery,
   the LOD band, and the batch-run traps re-learned there.
4. The K4 ledger (`2026-07-12-slice-k4-ledger.md`) — the SimHost two-event
   contract (`Loaded` vs `TimeChanged`) every new surface must ride; timeline
   interactions.
5. **The contract-economy spec** (`2026-07-12-contract-economy-design.md`,
   incl. its "Implementation amendments") + `docs/design/economy/markets.md`
   + `docs/design/interpolity/war.md` §Front supply lines — the model Phase 2
   surfaces. The CE ledger's C17 carried-debt list has atlas-relevant caveats
   (relay bids kept, fee-blind courier ranking).
6. The BF + CU-3 + CU-4 designs (`2026-07-16-bf-bank-flow-design.md`,
   `2026-07-17-cu3-currency-consolidation-design.md`, the CU-4 spec) — the
   monetary measures Phase 3 surfaces (reserve, `ClaimOnState`, backing
   ratio, numeraire rates, BackedShare). The REPL polity currency line is the
   parity target.
7. The DX design + ledger (`2026-07-16-domain-hex-expansion-design.md`,
   `2026-07-16-slice-dx-ledger.md`) — outposts, satellite workings,
   graduation; `SimState.Outposts` has ZERO reads in `src/Core/Atlas` today.
8. The L2 ledger (`2026-07-15-slice-l2-ledger.md`) — off-lane routing,
   detection, patrol coverage falloff.
9. `docs/HANDOFF.md`, `CLAUDE.md`.

Key code seams (re-verify against the current tree, as always):
`src/Core/Atlas/` (all lenses/panels/queries — see the spec's inventory),
`src/Inspector/EpochMapView.cs` `TradeCells` (moves INTO Core.Atlas),
`src/Inspector/Repl.cs` (`ebook`/`econtracts`/`efreight`/`domain`/currency
lines — every parity target), `unity/Assets/Atlas/DomainFieldLayer.cs`,
`SystemStage.cs` (the `OrbitRef` alias needing its overdue compile check),
`src/Core/Epoch/WorldEvent.cs` (`OutpostFounded = 314`, `CargoSeized = 409`).

## The four phase gates (spec §1–§4) — each independently mergeable

1. **Domain interior (DX)** — `DomainInteriorQuery`, DomainFieldLayer interior
   fill, subordinate named outpost marks, founded/graduation events in news.
   SystemStage already renders satellite hexes' works — verify, don't rebuild.
2. **Economy/trade (the old K6)** — TRADE lens (port `TradeCells`, keep the
   saturation filter, Inspector calls the query afterward), order-book +
   contracts panels via DockKit, freight purposes (courier / war convoy /
   spread run / state haul), war-supply readout (forward depot; contested-lane
   shading only if a cheap read-only presence query lands — never duplicate
   `ShipmentOps` rules).
3. **Currency & banking (CU/BF)** — currency-zone tint mode on the existing
   polity/domain rendering (unions share a tint), PolityPanel monetary block
   (currency, rate + drift, reserve, backing ratio), MarketPanel prices state
   their currency, RelationsPanel names credibility where CU-4's term
   participates.
4. **Off-lane, events, debt sweep (L2 + cheap debt)** — off-lane crawls
   rendered distinctly (direct hex-path, dashed/attenuated), patrol-coverage
   readout, event readthrough of all new types, the `OrbitRef` editor compile
   verification, AtlasSmoke extended to every lens/mode.

**Per-phase eyeball** (user decision — a deliberate widening of the standard
single taste gate): each phase ends with a short editor look before the next
begins. The spec names each phase's eyeball script. Scope nod and merge
decision stay single, as ever.

## Gates (every phase, all mechanical, all mandatory)

- `dotnet test StarSystemGeneration.sln` green with the golden **asserted
  byte-untouched** (the K-slice invariant — assert it, don't assume it; zero
  sim behavior means zero).
- Determinism byte-identity for same config.
- EditMode suite green (`LegendDriftTests` extended per new lens/mode + new
  panel tests) · `AtlasSmoke` renders every lens including TRADE and the
  currency mode.
- REPL surfaces still match their Core queries (parity is the point — where a
  derivation moved into Core.Atlas, the REPL now calls it).

## Boundary — NOT this slice (spec §Boundary)

No sim behavior of any kind · no play-clock trading UI · no perceived-books
work · readability deep-dives / labels-on-stage / timeline branch UI /
keyframe memory stay filed · the flat/sparse-economy pass is untouched (if
the new surfaces make the economy's flatness visible, that is *evidence* for
that pass, not scope here).

## Worktree / environment traps (verified through K4/K5/L/DX)

Copy gitignored `unity/Packages/manifest.json`, `packages-lock.json`,
`src/Core/csc.rsp` into the fresh worktree BEFORE any build/batch run.
**Batchmode dies in ~2s (exit 1, ~1KB log) while an editor holds the project —
and a trailing `echo exit: $?` masks the failure; verify log size + output
mtimes.** The editor MCP bridge starts revoked per-project. Goldens are CRLF
on disk. Vertex colors need explicit `.linear` in the linear pipeline (the K5
washed-palette bug). Windows worktree removal can fail with "Filename too
long" on `Library/PackageCache` — use a `\\?\`-prefixed `cmd /c rd /s /q`
fallback. PowerShell mangles piped REPL stdin — use bash `printf`. Every new
`src/Core` file gets a two-line `.meta` with a fresh guid; `unity/
ProjectSettings` churn stays uncommitted. `git log main` before merge-out.

## Model usage (per CLAUDE.md)

Subagent-driven-development throughout — Sonnet default. Opus-escalation
candidates: the `TradeCells` port (the one derivation-move with drift risk),
`DomainInteriorQuery` (spans registry/facility/candidacy reads), and any task
that touches the SimHost event contract. **Dispatch implementation subagents
synchronously (`run_in_background: false`) and verify with `git log`** — the
standing trap. One fresh-eyes whole-branch review (pinned `model: fable`)
before merge, one fix wave.

## Wrap-up, in order (per CLAUDE.md)

Merge to main locally → **push (push-on-merge is the standing default,
2026-07-20)** → update `docs/HANDOFF.md` → republish the living diagram
(`docs/diagrams/unity-atlas-design.html` §8/§9 rows — see the
`unity-atlas-design-artifact` memory for the stable URL procedure) → write the
next slice's kickoff prompt (or name the natural next step if a forced
kickoff doesn't fit) → sync Trello (move the Slice AC card to Merged; retire
the superseded K6 framing anywhere it lingers).
