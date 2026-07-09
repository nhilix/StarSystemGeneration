# Slice D Kickoff — Session Prompt

> **Status: half-drafted.** Written at Slice C's wrap-up with the C surface
> concrete. The Slice B session MUST complete every `[B session: …]` block at
> its own wrap-up before this prompt is used. Do not start D against an
> unfinished B.

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
4. **What Slice B landed**: [B session: fill in — registry types and files,
   port establishment chain, lane representation, artifact format/sections,
   what replaced `RegionCell`/where the natural raster lives now, deleted
   prototype inventory, RollChannels appended, ledger path + surprises.]
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
4. REPL surface: [B session: reconcile with what B's REPL exposes] — at
   minimum a `market <port>` dump (per-good price/qty/grade + black book) and
   a price-map layer; the eyeball gate is "blockade a lane, watch the spike".
5. User gates: scope nod · REPL eyeball · merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice E kickoff prompt** · flip the
   box below · push only on user say-so.

- [ ] Slice D complete
