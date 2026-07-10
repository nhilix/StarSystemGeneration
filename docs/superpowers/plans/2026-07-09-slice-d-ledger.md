# Slice D Ledger — Segments & Markets (slice-d-segments-markets)

Ordered task checklist per the kickoff prompt
(`2026-07-09-slice-d-kickoff-prompt.md`). Updated and committed as tasks
complete — the resumability record. D animates the economy over B's state
model (`src/Core/Epoch/`) and C's catalogs (`src/Core/Substrate/`): the
first slice where goods actually move. Design sources: `economy/markets.md`,
`substrate/market-geography.md`, `polity/population-and-identity.md`,
`economy/assets-and-investment.md`.

Architecture decisions (made at kickoff, flag deviations):

- **Market registry**: one `Market` per port (market id = port id), per-good
  arrays over the 17 `GoodId`s: price, inventory (qty + mean grade),
  last-cleared qty, black-book demand/price. Unsold inventory persists —
  gluts are visible state.
- **Credits are the treasuries**: B's Expansion/DevelopmentPoints stay as
  earmarked budget shares; the income source becomes real (tax + tariffs +
  state facility income), replacing `StubIncomePerPortPerYear`. Polities get
  a `Credits` ledger; conservation from genesis/entry endowments onward.
- **The economy is roll-free**: prices, flows, migration, drift are all
  deterministic functions in fixed iteration order (ports/goods/segments by
  id). No new `RollChannel` expected; if one becomes necessary it appends at
  41.
- **Segments deepen in place**: + CultureId (culture registry seeded one per
  species at genesis), 4-axis ideology distribution, SoL, Wealth. Migration
  creates/merges same-(species,culture) segments at destination ports —
  diasporas fall out of non-blending.
- **Facility→market attachment is derived**, never stored: nearest same-owner
  port servicing the facility hex (tie: lower port id).
- **Manual lane-cut hook**: transient `SeveredLanes` on SimState (debug-only,
  not serialized) + `estep` REPL continuation — the blockade-spike eyeball.
- **Artifact**: config layer v2 (EECO grows), actors v2 (policies + credits
  become real state — serializer comment anticipated this), segments v2;
  new `markets` layer (MARKET/BLACKBOOK/RESERVE/LOAN records) appended at the
  end of the layer list. Golden regenerated deliberately at slice end.

## Tasks

- [x] 0. **Branch + ledger** — branch `slice-d-segments-markets` from main;
      this file.
- [x] 1. **Economy state types + knobs** — `Market` (per-good price/
      inventory/grade/last-cleared/black book), `Culture` registry,
      `PopulationSegment` v2 fields (CultureId, SoL, Ideology[4], Wealth),
      `PolityRecord.Credits` + `Reserves` (stockpile) + `Loan` records,
      `EconomyKnobs` expansion (per-capita band rates, price drift limits,
      initial prices/endowment, labor share, freight/fuel costs, credit
      dials), event types 202+ (FamineStruck, FacilityBuilt, LoanIssued,
      LoanDefaulted, …). Gate: solution green, structural tests.
- [x] 2. **Supply lands** — MarketsPhase production execution: facility →
      market attachment; extraction output with terrain potentials + raw
      grade at the facility hex; processing consumes market inventory via
      recipes (tech-tier stub gates variants); organic baseline; labor share
      of revenue → segments, remainder → owner; condition scales output.
      Gate: unit tests (fixtures over a seeded state).
- [x] 3. **Demand assembles + price + clearing** — per-segment band demand
      (C profiles × config rates × income budget, embodiment-modulated);
      institutional demand (facility inputs/upkeep, polity procurement);
      priority-order clearing; transaction tax; price drift toward clearing
      (rate-limited, elasticity from band mix); famine/SoL consequences
      written to segments; black-book conversion for prohibited goods
      (GenesisController writes species-derived law codes). Gate: unit tests
      incl. priority order, famine flag, drift bounds.
- [x] 4. **Freight** — re-export demand term; arbitrage flows over lanes
      within `LaneMath.Capacity` (fleet-capacity stub), fuel + tariff costs,
      legality at both ends; contracts: unmet polity stockpile targets post
      premium contracts freight fulfills; internal logistics: polity moves
      own goods between own ports at cost (endpoints only touch markets).
      Gate: converge/diverge/blockade-spike tests, capacity cap test.
- [x] 5. **Allocation rework** — real income (tax + tariffs + facility
      income) × budget weights replaces stub income (knob deleted);
      facility siting execution (C `Siting.Score` + local price signal,
      goods bought at market, construction time, `FacilityBuilt`); upkeep
      purchases, condition decay when unmet; stockpile purchases toward
      targets with perishability decay; simple credit (borrow when treasury
      short, service, default event + collateral seizure). Colony valuation
      gains the price signal. Gate: unit tests, conservation test.
- [x] 6. **Interior rework** — demographics: growth = f(SoL, provisions
      access, embodiment), famine shrink, machine populations grow by fab
      consumption (Machinery + Compute); migration basics along SoL/income
      gradients over lanes (same-polity + refugee variant minimal);
      ideology drift from lived conditions. Gate: unit tests.
- [x] 7. **Artifact format v2** — config v2 (EECO grows), actors v2
      (PolityPolicies + Credits serialized), segments v2, new `markets`
      layer (MARKET/BLACKBOOK/RESERVE/LOAN + CULTURE records). Gates:
      byte-identity, load-vs-rebuild equivalence, version refusal,
      round-trip of typed payloads.
- [x] 8. **REPL surface** — `market <portId>` dump (per-good
      price/qty/grade + black book + segments with SoL/income);
      `emap price [good]` layer; `lanecut <portA> <portB>` debug hook;
      `estep [n]` continuation; help text. Gate: piped-stdin smoke via bash
      printf.
- [x] 9. **Shape acceptance + full gates** — 276/276 green; credits conserve
      *exactly to the mint* across seeds (the ledger economy audits clean). — 40-epoch runs across seeds:
      prices bounded (no NaN/runaway spirals), populations bounded, credits
      conserved; hex-tier suite untouched; full `dotnet test` green.
- [ ] 10. **Fresh-eyes whole-branch review** subagent + one fix wave.
- [ ] 11. **USER: REPL eyeball** — the taste gate: run a sim, `market` a
      hub, `lanecut` a lane, `estep`, watch the spike at the strangled port
      and the glut at the producer. Tune knobs as directed.
- [ ] 12. **Golden freeze + wrap-up** — regenerate
      `Goldens/slice-b-artifact-seed42.txt` successor in the same commit as
      the final format · merge on user nod · HANDOFF · **write Slice E
      kickoff prompt** · flip kickoff checkbox · push only on user say-so.

## Notes / surprises

- **Bootstrap problem** (task 2): construction costs real goods only
  facilities produce — a cold-start deadlock. Resolution: homeworld entry
  seeds a starter industry (AgriComplex, Mine, Skimmer, Refinery, Foundry,
  tier 1, extraction ids before processing so the chain flows in one step)
  plus the one-time credit endowment (`InitialCreditsPerPolity`, the only
  mint). Genesis furniture — no FacilityBuilt events. **Flag at eyeball.**
- Facility→market attachment simplified to *nearest same-owner port* (tie:
  lower id) — the servicing check was redundant in practice.
- Organic baseline moved from task 2 (supply) to task 3 (demand): it is
  self-supply that offsets subsistence demand, not sellable inventory.
- Automation stays 0 in the labor factor until Compute-driven automation
  gets a consumer story (G); machinery grade defaults to the neutral 0.5
  when a market holds none.
- Golden regenerated per history-changing task (EECO knob line; starter
  industry; live market step) — deliberate, same-commit, history changes are
  the slice's point.
- **Wages precede sales** (task 3): the labor share is paid at deposit time
  from owner credits (households get same-step purchasing power — solves the
  income/spending circularity); suppliers recoup the full cleared value at
  distribution. Owner credits may run negative pre-credit (task 5).
- Law codes derive from `PerceptionView.SelfSpecies` (an actor perceives its
  own society) — no controller-ctor churn, reattaches cleanly on load.
  Genesis flavor: openness < 0.35 prohibits narcotics, < 0.55 restricts.
- `PopulationSegment.LastSubsistence` is a transient consequence (Markets
  writes, Interior reads same step) — never serialized; load-then-step
  recomputes it before any read.
- Mid-slice state (post task 3): colonies famine — no facilities (task 5)
  and no freight imports (task 4) yet. The famine wall receding as tasks 4–6
  land is the natural acceptance arc.
- **Design-doc amendment (task 4, flag to user)**: `markets.md` market-step
  order — freight now moves *before* the price drift, so the drift reads
  realized supply. With the doc's original order an import-fed port always
  priced against empty shelves (permanent artificial spike, wealth death
  spiral); with the amendment a blockade is what spikes, exactly as the
  design intends. Freight plans on the previous drift's prices.
- Task-4 mechanics born from the blockade diagnostic: demand is
  **wealth-backed** (poverty reads as glut, not frozen prices); arbitrage is
  **absorption-capped** (ships what the destination will take beyond stock —
  re-export demand makes hubs pull); prices carry an absolute ceiling
  (base × 1000) against ε-supply runaways; the organic baseline scales with
  embodiment (a lithic farms as little as it eats).
- Explicit escrowed contract objects deferred to E (carriers) / I (news
  propagation): D ships polity procurement (stockpile targets → purchases)
  + reserve release to starving own ports as the internal-logistics reading.
  **Flag at eyeball.**
- Freight fuel is monetized at the source market's fuel price and pulls on
  its fuel demand (movement is never free); physical fuel drawdown awaits E's
  hulls.
- **Macro lessons of task 5** (each found by running, not theorizing):
  supply is price-elastic (producers throttle to 15% utilization at floor
  prices — gluts clear instead of pegging); treasury spending pays
  **construction wages** (lane/tier/facility budgets → the building port's
  households) so investment recycles instead of destroying the money supply;
  colony founding recycles the expedition cost to the settlers and seeds one
  agri-complex (settlers farm first — expedition furniture, like the
  homeworld starter set); development pulls its own construction basket as
  market demand so freight hauls materials to the frontier. Money now mints
  only at entry (endowment 500) + homeworld founding wealth.
- Lane/tier costs stay credit-denominated (B's machinery); only facility
  construction consumes real goods — flag if the eyeball wants port
  infrastructure on goods too.
- Remaining famine texture is the two kinds task 6 is designed to relieve:
  rich-but-laneless ports (cannot import) and poor-with-food ports
  (poverty) — migration and famine demographics redistribute both.
- Task-6 notes: migration runs *before* demographics (refugees flee, then
  attrition bites whoever stayed); refugees escape laneless ports off-lane
  within colonization reach (same polity); port tier caps are shared across
  segments and gate migration inflow; machine growth reads LastSubsistence
  (their subsistence IS fab inputs — cut off, they age out); famine events
  chronicle at <0.75 (aligned with the shrink threshold — famine is when
  people die, scarcity is just prices); homeworld starter agri is tier 2 (a
  spacefaring species has long since fed itself); MigrationWave=206 events
  for refugee exoduses.
- The frontier is *harsh* at 40 epochs (roughly half of young colonies run
  subsistence deficits and shed refugees). All mechanics present; the
  world's mood is a knob-tuning conversation for the eyeball gate.
- Task-7 surprise: `LastSubsistence` looked transient but reserve release
  reads the *previous* epoch's value — cross-step state, so segments v2
  serializes it (the load-then-continue byte-equivalence test would have
  caught the divergence).
- Task-8 macro wave (the REPL dump made the pathologies visible):
  **industry inputs are demand** (upkeep + planned recipe draws register at
  the attached market — without this machinery floors and the industrial
  base rots); **working capital** (production input purchases aren't
  credit-clamped; revenue lands the same step, insolvency is Allocation's
  credit problem); **condition drifts toward the met fraction** (partial
  upkeep = partial health, never an unrecoverable floor) with **pro-rata
  upkeep** per market (chains recover together); **receipts-based
  budgeting** (`Receipts` step-transient on PolityRecord: Allocation budgets
  the epoch's receipts even under a negative balance — deficit-financed
  development ends the liquidity-trap stall); price ceiling ×1000 → ×100
  (ceiling-price churn was minting paper fortunes via the wage share).
  Households still accumulate large paper wealth through spike eras —
  a tuning conversation for the eyeball gate.
