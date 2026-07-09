# Slice C Kickoff — Session Prompt

You are starting **Slice C (Substrate catalogs)** of the epoch-sim
implementation roadmap, under the lighter protocol in `/CLAUDE.md` (read it
first). C is **pure Core data and functions** — no sim contact, no state model
— which is why it can run in parallel with Slice B or standalone. Implement
directly from the design tree; no plan document.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — meta-plan.
3. **The design docs C implements**:
   `docs/design/substrate/commodities.md` (17 goods, the Grade system,
   recipes/variants, demand profiles, legality),
   `docs/design/substrate/infrastructure.md` (14-type catalog, siting rules,
   the production formula),
   `docs/design/substrate/market-geography.md` (potentials from raster fields).
4. **What Slice A landed**: `src/Core/Epoch/` — namespace conventions,
   `IsExternalInit.cs` shim (records on netstandard2.1), the ledger's
   notes/surprises (`docs/superpowers/plans/2026-07-09-slice-a-ledger.md`).
   C's catalogs live beside it (suggested: `src/Core/Substrate/`,
   namespace `StarGen.Core.Substrate`). Policy/act records in
   `src/Core/Epoch/Policies.cs` key goods by int id — C's good ids are what
   those keys will mean.
5. Reference for style: `src/Core/Content/*.cs` (existing catalog pattern —
   static tables, data as code), `src/Core/Tables/WeightedTable.cs`.

## Scope (roadmap row C)

- **Commodity catalog**: the 17 goods with stable int ids, Grade,
  recipes/variants, per-good demand profiles (use-case consumption),
  legality schema.
- **Infrastructure catalog**: the 14 types with stable ids, siting rules
  (what wants belts, what wants port hearts), the production formula as a
  pure function.
- **Potentials**: extraction/production potential functions over the natural
  raster fields (density, lean, metallicity, anchors) — pure functions of
  cell inputs, unit-tested standalone.

**Boundary**: data + pure functions only. No prices, no markets, no trade
(Slice D); no facility registry or siting *execution* (Slice B/D own state);
nothing imports `SimState` or the prototype. If B has already slimmed/renamed
raster types, build against those; otherwise take cell field values as plain
arguments so C stays decoupled either way.

New Unity `.meta` files are required for any new folder/file under `src/Core`
(see A's ledger note). Append any new `RollChannel` values (unlikely for C —
catalogs are roll-free); never renumber.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-c-substrate-catalogs`; task ledger
   (`docs/superpowers/plans/YYYY-MM-DD-slice-c-ledger.md`); TDD; frequent
   commits. The two catalogs are genuinely independent lanes — parallel
   subagents pay here if the session wants them.
3. Gates: `dotnet test` green (hex-tier untouched); catalog determinism is
   trivially structural (data), so the tests are invariants: id stability,
   recipe closure (every input is a catalog good), siting/production formula
   fixtures from the design doc's worked examples.
4. REPL surface: a `goods` / `infra` command dumping the catalogs and
   evaluating potentials for a sample cell — the eyeball gate is "the catalog
   reads like the design doc's tables".
5. User gates: scope nod · REPL eyeball · merge decision.
6. Wrap-up: merge · HANDOFF · update the Slice D kickoff expectations (D needs
   B+C) · flip the box below · push only on user say-so.

- [x] Slice C complete
