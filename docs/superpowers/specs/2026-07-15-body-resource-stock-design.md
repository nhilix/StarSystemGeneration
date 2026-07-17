# Body resource stock — design (2026-07-15)

Reopens part of the locality mega-slice design
(`2026-07-14-locality-mega-slice-design.md` §4) after the Slice L final
whole-branch review and its own fix wave revealed a deeper problem than
either caught: extraction's "throughline" — real body-level richness
reaching the price signal — was never actually built. The fix wave (commit
`c107587`) corrected `RichnessModifier`'s bounds and formula math, but the
review that approved it, and the brainstorming session that produced this
doc, established that the formula was the wrong shape entirely: it's a
bounded `[0.5, 1.5]` multiplier layered on top of `MarketEngine.SupplyLands`'s
unchanged, pre-slice `ExtractionPotential(type, good, fields)` computation —
and `fields` is hex-aggregate `CellFields` (regional raster: `MeanDensity`,
`Lean`, `Metallicity`, two anchor flags), never body data. The base extraction
math stayed exactly what it was before Slice L; a body-shaped decoration was
added on top, one that goes fully inert (neutral `1.0`) whenever a facility's
`Body` is `None` or the resolved body kind carries no per-body variance
(`PlanetoidBelt`/`Wreckage`, both hardcoded `Size = 0` by the generator).

This was surfaced by a real, reproducible artifact of the *existing* (pre-Slice-L)
genesis pipeline, found during the REPL/Unity eyeball gate: `Siting.Score` for
extraction types (`src/Core/Substrate/Siting.cs:39-43`) ranks candidate hexes
purely from regional raster potentials (`Potentials.Ore`/`Volatiles`/
`Biosphere`/`Exotics`, `src/Core/Substrate/Potentials.cs`) — entirely decoupled
from `BodyGenerator.Generate`'s independent per-slot roll of whether any body
exists there at all (`src/Core/Generation/BodyGenerator.cs:32-35`,
`BodyTables.Kind.Pick(...)` can return `null` per slot). A hex can score well
for mining and, once actually generated, have zero bodies in every orbit slot.
This predates Slice L — the pure generator has always been able to produce
this — but Slice L's atlas work (Task 7) made it visible for the first time by
rendering the real committed system instead of a fresh per-render guess that
degraded to the same fallback silently.

## Scope

This design covers what a facility actually extracts from, replacing the
richness-multiplier mechanism entirely for the four extraction types (Mine,
Skimmer, AgriComplex, ExcavationSite). It does not reopen anything else in
Slice L (`BodyRef`, `SettledSystems`, claim-aware siting at groundbreaking,
the atlas read path) — those stay as built. Facility siting's own ranking
(`Siting.Score`) is unchanged; only what happens *after* a hex is chosen and
its real system is committed changes.

Explicitly out of scope (deferred, consistent with Slice L's own boundary):
facility relocation/decommission when a body depletes; making `Siting.Score`
itself body-aware (would require generating every candidate's full system
just to rank it — the cost problem Slice L's design already rejected);
adjacent-hex spillover when a hex's bodies are all claimed (a separate,
already-deferred follow-on from Slice L's Task 6 review).

## The model: two kinds of extraction, split by what the resource actually is

**Mine and ExcavationSite — finite, depletable.** Ore and exotic salvage are
inherently exhaustible: you dig up a fixed amount and it's gone. A rock has
1000 units of iron ore; a mining platform extracts up to its rated capacity
per year until the rock runs dry, then it's dry — mechanically, "a rock has a
resource value, exploited by a facility," not "a hex has an aggregate
potential."

**Skimmer and AgriComplex — renewable, no depletion.** A gas giant's mass is
effectively inexhaustible at any facility's extraction scale; farming a living
biosphere is a sustainable process (the soil replenishes), not a one-time
dig. These draw a yield rate from the claimed body's own real, already-varying
attributes (`GasGiant.Size`, `Biosphere`/`Hydrographics`) — real per-body
variance, permanently, with no stock to run out.

Both replace `CellFields`/`ExtractionPotential` as the thing production reads
from. The body is the source of truth for all four types; regional raster
fields (`Potentials.*`) stay relevant only to `Siting.Score`'s pre-generation
ranking and to biasing a depletable body's initial roll (below) — they never
drive ongoing production again.

## `BodyResources`: the depletable-stock registry

A new epoch-tier registry, `SimState.BodyResources`:
`Dictionary<(HexCoordinate, BodyRef), Stock>`, reusing the existing
`Stock` struct (`src/Core/Substrate/Grades.cs`: `(GoodId Good, double
Quantity, double Grade)`) — no new value type. In-memory during a run,
genuinely serialized (this is real mutable state, not re-derived hex tier).

**Rolled lazily, once**, in `ProjectOps.SpawnFacilityConstruction`, the
instant `BodySiting.Assign` resolves a real body for a *new* Mine or
ExcavationSite facility — the same extension point Task 6 already built.
Mirrors `SystemRegistry.Commit`'s "memoize on first real touch" idiom,
keeping state growth tied to actual mining activity, not every body in every
settled system (the same growth discipline `Settlement.SettledHexes` already
tracks).

**The roll**: the region's existing raster score (`Potentials.Ore` for Mine,
`Potentials.Exotics` for ExcavationSite — the same score `Siting.Score`
already uses to rank the hex) sets the *expected* quantity via a knob-scaled
formula; a deterministic hash keyed `(hex, StarIndex, SlotIndex, channel)`
(the project's stateless-roll discipline) gives real variance around that
expectation, so two belts in the same rich hex differ. Grade derives the same
way, reusing `Potentials.RawGrade`'s shape. Two new knobs: an ore-quantity
scale and a variance-spread factor.

**Depletion**: each step, `MarketEngine.SupplyLands` extracts
`min(rated capacity via the existing Production.Output tier formula,
remaining Stock.Quantity)`, decrements the registry entry, and values the
output through the *existing* `Grades.Multiplier(useCase, grade)` pipeline
against the stock's grade — no new grade math. At exactly zero, the facility
simply produces `0` for that good from then on — it falls out of `IsActive`'s
revenue naturally, the same as any other unprofitable facility today. No new
idle/decommission state.

## No eligible body: construction fails, not a permanent dud

The hex-(-3,4) case: `BodySiting.Assign` returns `BodyRef.None` for any of
the four extraction types (no body of the needed kind exists, or the system
has no bodies at all). Groundbreaking **aborts** at that exact point — no
`Facility`, no `Project` is created. This is the shared check for all four
extraction types, not just the depletable two: a Skimmer or AgriComplex with
no gas giant / no biosphere body to draw attributes from has nothing to
localize to either.

This mirrors real prospecting failing sometimes — a bad-body hex simply
doesn't get built on, rather than sitting in state forever as a facility that
can never produce anything. `Siting.Score`'s ranking stays cheap and
body-blind exactly as the original design chose; the correlation between
"this hex scored well" and "this hex actually has something to extract" is
imperfect by design, and this is where that imperfection surfaces and gets
resolved — as a failed attempt, not a standing wound in the sim state.

Whether the same hex gets retried by the actor's plan next cycle, or the
planner naturally moves to a different candidate, is existing `Planner.cs`
plan-entry-lifecycle behavior this design doesn't change — worth confirming
during implementation planning, not a new mechanic to build.

## No migration path

Every simulation run in this project starts fresh from a seed
(`EpochGenesis.Seed` + `EpochEngine.Run`); there is no supported "load an
artifact produced by an older build" workflow (greenfield discipline: no
compatibility adapters, no cross-slice artifact preservation — goldens
re-freeze once per slice, by design). Once this lands, every Mine/
ExcavationSite facility that will ever exist was created through the
(modified) `SpawnFacilityConstruction`, which rolls its stock at that exact
moment. There is no facility that could exist without a stock entry, so there
is nothing to migrate.

## Determinism, conservation, testing

**Determinism**: the stock roll is a stateless hash keyed `(hex, StarIndex,
SlotIndex, channel)` — deterministic, fixed-order, no shared mutable RNG
state. `BodyResources` iterates sorted `(hex, body)` for any serialized or
diagnostic output (P6).

**Conservation (P4)**: depletion is a real resource leaving a real stock —
not a mint, not a sink from nowhere. Value still flows through the existing
wage/sale paths in `SupplyLands`; extraction is capped by remaining quantity
so a facility can never produce more than the body actually has left.
`ConservationTests` must stay green.

**Testing**: `BodyResourceOpsTests` — idempotent commit (reference equality
on repeat calls, mirroring `SystemRegistryTests`), regional-richness-weighted
variance across repeated rolls, depletion arithmetic (extract → decreases →
floors at zero, never negative). `ProjectOpsTests` (extended) — a
construction candidate whose committed system has no eligible body aborts
cleanly for all four extraction types. `MarketEngineTests`/
`ConservationTests` — extraction respects remaining stock as a hard ceiling;
Skimmer/AgriComplex yield varies with the claimed body's own real attributes.
Golden re-frozen once, at the end of this work.

## Documentation to amend (in-branch, per CLAUDE.md — the design is the spec)

- `docs/design/substrate/infrastructure.md:60-61` — the "Terrain" line
  currently reads "extraction reads the genesis fields at its hex — output
  *and grade* root in geography." This is exactly the mechanism this design
  replaces; rewrite to describe body-native stock/yield.
- `docs/design/substrate/commodities.md:49-50` — `Stock`'s "wherever stocks
  live (markets, stockpiles, cargo holds)" list gains body reserves as a
  fourth home for the same `(good, quantity, grade)` shape.

## Provided interface

- `SimState.BodyResources`: `Dictionary<(HexCoordinate, BodyRef), Stock>` —
  the depletable-stock registry for Mine/ExcavationSite bodies.
- `BodyResourceOps.Commit(state, hex, body, type, system)`: idempotent
  first-touch stock roll, mirroring `SystemRegistry.Commit`.
- `ProjectOps.SpawnFacilityConstruction` now rejects groundbreaking outright
  (no Facility/Project) when any of the four extraction types resolves no
  eligible body.
- `MarketEngine.SupplyLands` extraction for Mine/ExcavationSite reads and
  depletes `BodyResources`; for Skimmer/AgriComplex reads the claimed body's
  own attributes directly. `RichnessModifier` and `CellFields`-based
  `ExtractionPotential` are retired from the extraction path entirely.

## Amendment (2026-07-15, Slice L2) — agri yield floor + per-resource-class claims

Two refinements landed in Slice L2 (colony-founding follow-ups), user-approved,
amending this design in-branch:

**(a) AgriComplex renewable yield has no floor.** `BodySiting.RenewableYield`
for AgriComplex now derives purely from the body's biosphere and water —
`Clamp01(0.7·bio + 0.3·water)`, `bio = (int)Biosphere/3`, `water = Hydrographics/100`
— and reaches **0 at a fully barren, dry body**. A barren rock genuinely can't
farm, so colony founding (and groundbreaking) sites no subsistence AgriComplex
there rather than shipping equipment for a bodiless dud. This is a **deliberate
asymmetry** with the Skimmer branch, which *retains* its 0.5 mass floor: any gas
giant has mass to skim (physically justified), whereas a barren rock has no
biosphere to farm. (Consequence: a mineral colony at a system whose bodies are
all Barren founds a Mine but **no** farm — correct, not a regression; the old
0.3 floor manufactured a non-functional farm facility there.)

**(b) Body claims are per-resource-CLASS, not global.** A single body can host
multiple facilities of *different* resource classes: one rich rocky world runs a
Mine depleting its ore **and** an AgriComplex farming its (renewable) biosphere
at the same time — the two never touch each other's resource (`BodyResourceOps.Commit`
no-ops for Skimmer/AgriComplex, so an Agri sharing a Mine's body never reads the
Mine's stock). Contention is now within a class only:
- **Mine ↔ ExcavationSite** (both depletable) exclude each other and themselves:
  the depletable stock is keyed `(hex, body)` with a single good, so a second
  depletable extractor on one body would read the wrong good — they *must* stay
  mutually exclusive.
- **Skimmer ↔ Skimmer**, **AgriComplex ↔ AgriComplex** exclude (the two-mines
  fix, generalized to each renewable class).
- **Two non-extraction** support assets exclude (both ride the port body —
  unchanged).
- **Any cross-class pair** (Mine↔Agri, Skimmer↔Mine, …) does **not** contend.

New surface: `BodySiting.CompetesForBody(InfraTypeId a, InfraTypeId b)` encodes
the rule; `ProjectOps.PlaceFacilityBody`'s claim scan now collects a placed
facility's body as "taken" only when it competes with the type being placed
(was: every co-located body, globally). `BodySiting.Assign`'s contract is
unchanged — the caller still passes the (now class-filtered) claimed set.
