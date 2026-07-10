# Slice D Kickoff — Session Prompt

> **Status: complete.** C half written at Slice C's wrap-up; B half filled at
> Slice B's wrap-up (both merged to main 2026-07-09).

You are starting **Slice D (Segments & markets)** of the epoch-sim
implementation roadmap, under the lighter protocol in `/CLAUDE.md` (read it
first). D animates the economy: population segments, one market per port, the
price engine, freight, household income, stockpiles, simple credit. D consumes
B's state model and C's catalogs; it is the first slice where goods actually
move.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — meta-plan
   (row D; between B and D the sim was expansion-only by design).
3. **The design docs D implements**:
   `docs/design/economy/markets.md` (price formation, clearing, re-export),
   `docs/design/substrate/market-geography.md` (market-per-port, black book,
   connectivity-as-price-structure, perception-gated prices — perfect-info
   until Slice I),
   `docs/design/polity/population-and-identity.md` (segments, two identity
   layers, demographics, migration basics),
   `docs/design/economy/assets-and-investment.md` (stockpiles, credit,
   household income),
   `docs/design/substrate/commodities.md` + `infrastructure.md` (what C built
   the vocabulary for).
4. **What Slice B landed** (`src/Core/Epoch/`; ledger
   `docs/superpowers/plans/2026-07-09-slice-b-ledger.md` — read its notes):
   - **State model**: `SimState(EpochSimConfig, GalaxySkeleton)` carries the
     natural raster plus id-ordered registries — `Ports` (`Port`: owner,
     hex, mutable `Tier` 1–3, `FoundedYear`), `Lanes` (`Lane`: ordered
     `PortAId < PortBId`, `BuiltYear` — a port-pair record, no cell path),
     `Facilities` (shape only: `TypeId` = C's `InfraTypeId`, `Condition` —
     **D executes siting/construction into this registry**), `Fleets`
     (record stub for E), `Segments` (`PopulationSegment`: `PortId`,
     `SpeciesId`, mutable `Size` — **D deepens into the two identity
     layers**), `Polities` (`PolityRecord`: `SpeciesId`, `ExpansionPoints`,
     `DevelopmentPoints` — **D replaces the stub income filling these**).
     `state.PolityOf(actorId)` looks up the record.
   - **Derived geography** (never stored): `PortDomains.ServiceRadius/
     Services/OwnersAt/IsContested` (radius 4 + 4/tier hexes; void cells
     never serviced); `LaneMath.InterPortRange/InRange/Capacity/
     TransitSpeed` (reach 18 + 8/tier, min of both ends; capacity =
     tier-sum × 0.5) — **D's freight routes over `Capacity`**.
   - **Expansion chain**: Perception fills `PerceptionView.ExpansionPoints`
     + `.ColonyCandidates` (`ColonyValuation.CandidatesFor` — terrain
     scoring; **D adds the price signal to the score formula**);
     `GenesisController` founds when affordable; Resolution's
     `TryFound` establishes tier-1 ports (homeworld hexes reserved);
     Allocation accrues `StubIncomePerPortPerYear × budget weights`
     (**the stub D's Markets replaces**), builds lanes nearest-first,
     raises tiers lowest-first; Interior enters polities (homeworld = first
     port at tier 2) and grows segments logistically toward
     `Tier × SegmentCapPerTier`. Knob families:
     `EpochSimConfig.Infrastructure` / `.Expansion`.
   - **Artifact**: `ArtifactSerializer.ToText/Save/Load` — 11 layers
     (config/clock/raster/species/actors/ports/lanes/facilities/fleets/
     segments/events), **versioned per layer — D bumps the layers it
     extends** (segments, facilities; new markets layer appends to the
     list), both configs stamped, typed event payloads, controllers
     reattach on load, truncation refused. Golden:
     `tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`
     (+ `GoldenTests`) — **regenerate deliberately in the same commit when
     D legitimately changes history**.
   - **Raster**: `RegionCell` is natural-only (density, void, chokepoint,
     lean, metallicity, anchors); `GalaxySkeleton` = cells + species;
     `SkeletonBuilder.Build` = seeding passes only, no sim. Adapt C's
     `CellFields`/`CellSite` from it at call sites (see the `infra` command
     in `Repl.cs` for the pattern).
   - **Deleted** (git history is the archive): prototype `EpochSim`,
     `Sim/*`, `Polity`, `War`, `GalaxyEvent`, v5 `SkeletonSerializer`,
     per-cell political fields, Inspector political layers.
   - **Events**: political 300s (`PolityEmerged=300`, `PortEstablished=301`),
     economic 200s (`LaneOpened=200`, `PortTierRaised=201`) — **D appends
     into the 200s** (next free: 202). `RollChannel` next free: **41**
     (40 = entry schedule; 37–39 retired).
   - Surprises worth knowing: homeworld anchor hexes are reserved from
     colony valuation (entry would double-found); segments founded on an
     entry step start integrating the following epoch; every new
     `src/Core` file needs a `.meta` (two-line format, fresh guid).
5. **What Slice C landed** (`src/Core/Substrate/`, namespace
   `StarGen.Core.Substrate`; ledger
   `docs/superpowers/plans/2026-07-09-slice-c-ledger.md`):
   - `GoodId` (frozen ids 0–16) · `Goods.All` / `Goods.Get(id)` ·
     `Recipe(Output, Kind, Inputs, GradeBase, MinTechTier)` with
     Standard/Advanced variants · `GoodQuantity(Good, Quantity)`.
   - `Stock(Good, Quantity, Grade)` + `Stock.Blend` ·
     `Effective(UseCase)` · `Grades.Output(recipe, meanInputGrade,
     facilityTier, techTier)` (tech multiplies *and* ceilings) ·
     `Grades.BandOf` / `TechCeiling` / `PrecursorFloor`.
   - `DemandProfiles.Population(Embodiment, PopulationBand)` /
     `.Institutional(UseCase)` / `.PriorityOrder` / `.SubsistenceScale` —
     **weights are normalized shares**; D supplies the absolute per-capita
     rates from `EpochSimConfig.Economy` and multiplies.
   - `GoodLegality(LegalityLevel, Tariff)` — D wires polity law codes
     (`PolityPolicies.LawCode`, int-keyed by `GoodId`) into market clearing
     and black-book demand.
   - `Infrastructure.All` / `.Get(InfraTypeId)` (frozen ids 0–14) ·
     `Production.Output/LaborFactor/TierOutputFactor/TierCostFactor/
     OrganicBaseline` · `Potentials.Ore/Volatiles/Biosphere/Exotics/RawGrade/
     EmbodimentAffinity(CellFields)` · `Siting.Score(type, CellSite,
     workforce)` — `CellFields`/`CellSite` take plain field values; adapt from
     B's cell type at the call site.
   - Catalog quirks D must respect: Luxuries has no producing facility until
     Slice G (corporate niche); catalogs are roll-free and stateless; the
     substrate never imports sim state — the dependency arrow points at it.

## Scope (roadmap row D)

- **Population segments**: two identity layers, demographics, migration
  basics, per-segment demand via C's profiles × config rates.
- **Market-per-port state**: price, last-cleared quantity, mean grade per
  good; black book for prohibited goods.
- **Price engine**: clearing, elasticity, drift, re-export demand;
  lane-connected markets arbitrage within freight capacity; disconnected
  markets diverge; blockades read as spikes/gluts.
- **Freight**: arbitrage / contracts / internal logistics over a
  fleet-capacity **stub** (Slice E replaces it with posted capacity).
- **Household income · stockpiles · simple credit** per
  assets-and-investment.md.
- **Facility siting execution** against C's `Siting.Score` + B's registries
  (construction consumes real goods per C's build costs).

**Boundary**: no fleets/postures (E); perception stays perfect-info (I); no
tech domains (G) — tech tier is a config-level stub; no corporations beyond
what the roadmap row names. New `RollChannel`s appended, never renumbered.
New Unity `.meta` files for any new file/folder under `src/Core`.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-d-segments-markets` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-d-ledger.md`; TDD; frequent
   commits. **Do not share a checkout with another live session — take a
   `git worktree`** (slice B/C collision, see C's ledger).
3. Gates: `dotnet test` green (hex-tier untouched) · determinism
   byte-identity for same config incl. market state · load-vs-rebuild
   equivalence · shape-acceptance bands for prices (no runaway spirals over
   40 epochs — assert bounded).
4. REPL surface: build on B's — `epoch <seed> [epochs] [radiusCells]` runs
   and stores the sim; `emap [domains|lanes]` renders it (add a price-map
   layer beside those); `chronicle [actorId]`; `esave`/`eload`. Add at
   minimum a `market <port>` dump (per-good price/qty/grade + black book);
   the eyeball gate is "blockade a lane, watch the spike" (a manual
   lane-cut hook is acceptable until H's real blockades).
5. User gates: scope nod · REPL eyeball · merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice E kickoff prompt** · flip the
   box below · push only on user say-so.

- [x] Slice D complete (merged to main 2026-07-09)
