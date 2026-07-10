# Slice F Kickoff — Session Prompt

You are starting **Slice F (Deep genesis)** of the epoch-sim implementation
roadmap, under the lighter protocol in `/CLAUDE.md` (read it first). F makes
the galaxy's past real: the cosmic clock (structure formation over the
region-cell lattice) and the evolutionary clock (life, sapience, precursors)
replace the painted seeding passes with simulated history. After F, density,
metallicity, stellar lean, biospheres, homeworlds, and *when each polity
enters* are all residue of causes — and the galaxy has archaeology.

## Read, in this order

1. `/CLAUDE.md` — workflow and hard rules.
2. `docs/superpowers/plans/2026-07-09-implementation-roadmap.md` — row F and
   the sequencing rationale (F was deliberately deferred past B/D/E so it
   builds against the settled state model; G consumes E's commander slots
   next, so F is a self-contained interlude on the *galaxy* side).
3. **The design docs F implements**:
   - `docs/design/genesis/cosmic-genesis.md` — the potential prior, the
     conserved field stack (Gas/StarsYoung/Mid/Old/Remnants/Metals),
     the ~100–150-step loop, discrete features (mergers, globulars,
     nebulae, AGN), the four-part provided interface, cost budget.
   - `docs/design/genesis/life-and-precursors.md` — the biosphere fields,
     the evolutionary loop, the **emergence schedule** (the headline
     output: staggered polity entry becomes causal), precursor waves with
     the coarse civ-arc sim, the four living-residue channels, the
     four-part provided interface.
   - Skim `frame/time.md` (the four clocks — F implements the first two)
     and `frame/space-and-travel.md` (the raster is nature's sampling
     grid; F writes it, never politics).
4. **What F replaces** (transition rule 2 finally fires):
   `src/Core/Galaxy/SkeletonBuilder.cs` — `PassStellarPopulation` (pass 2),
   `PassResourceAnchors` (pass 3), `PassHomeworlds` (pass 4) are the
   painted stubs; `PassDensitySummary` + the traversability/chokepoint
   pass survive in *shape* but should read the simulated present-day
   fields instead of the analytic paint (Tier-1 per-hex density stays a
   pure function — it just reads a persisted cell layer now,
   cosmic-genesis.md §Tier-1 consequence). `EpochGenesis.Seed` +
   `RollChannel.EpochEntrySchedule` (40) — the rolled entry stub the
   causal schedule retires. The **Phase-1 system/body generation suite
   stays green throughout**; seeding-pass *tests* are the one legitimate
   replacement zone (they test the stubs).
5. **What Slice E landed** (ledger
   `docs/superpowers/plans/2026-07-09-slice-e-ledger.md` — the notes
   carry hard-won conventions):
   - The full economy now runs on hulls: designs/lineages
     (`ShipDesign.cs`, `ShipCatalog.cs`), yards, six postures, posted
     freight capacity, supply/attrition/wreckage (`FleetOps.cs`), colony
     convoys. F does not touch any of it — but F's species profiles and
     emergence schedule feed `EpochGenesis.Seed`, entry designs
     (`DesignRegistry.RegisterEntryDesigns`), and starter fleets, so
     entry-time behavior is a contact surface.
   - **Knob discipline (hard)**: every calibration constant → a knob
     family + `KnobRegistry` (name-sorted, docs, tests enforce) +
     consequences row in `docs/TUNING.md`. F adds `Cosmic` and
     `Evolution` families (design docs list the dials). Catalog-style
     data (star tables, vigor classes, end-cause weights) is
     data-as-code with a TUNING structural note — the E chassis catalog
     is the pattern.
   - **Artifact** (`ArtifactSerializer.cs`): 12 layers, fleets at v2.
     F persists the cosmic outputs: expect raster v2 (new cell fields)
     plus appended layers (features, biospheres, emergence/precursor
     registry) — genesis outputs are P6-persisted, unlike the hex tier.
     The deep-time chronicle lands in the existing events layer: Cosmic
     (0–99) and Evolutionary (100–199) blocks are empty — F opens them
     (economic next free: 207, military: 403). Payloads need serializer +
     `SimTraceView` cases. Golden regenerated per history-changing task,
     frozen once at slice end; the strongest gate is
     `LoadThenContinue_EqualsTheStraightRun`.
   - **Determinism**: rolls keyed (step, cell/actor, channel);
     `RollChannel` next free **41** (append, never renumber) — the cosmic
     and evolutionary loops will want a block of channels; fixed
     spiral-index order everywhere.
   - **E lessons that will bite F too**: bootstrap by furniture where a
     chain can't self-start (starter industry/fleet/yard precedent);
     when a mechanism looks dead, check *availability* before tuning
     *magnitude* (the zero-shelf lesson); event/metric counts can mislead
     — report volumes; every new `src/Core` file needs a two-line `.meta`
     with a fresh guid; pipe the REPL via bash `printf`; Grep can render
     `//` as `\ ` — Read before "fixing" phantom comments.
   - **REPL**: `epoch/estep/emap/market/fleet/designs/chronicle/esave/
     eload/knobs/goods/infra` all live. `emap` layers: domains, lanes,
     traffic, price. `map` (galaxy-side) has density/lean layers — F's
     natural home for gas/metallicity/age/biosphere/emergence layers.

## Scope (roadmap row F)

- **Cosmic sim (0a)**: potential prior over the existing shape knobs;
  conserved field stack per cell; the inflow → transport → star formation →
  aging → death/enrichment loop; discrete features (mergers, globulars,
  emergent nebulae, AGN epochs); present-day derivations replacing painted
  `MeanDensity`/`StellarLean`/`Metallicity`; habitability history scalars;
  deep-time chronicle entries.
- **Life & precursors (0b)**: biosphere fields over the physical galaxy;
  abiogenesis/aging/catastrophe/spread/sapience loop; **the emergence
  schedule** (spaceflight dates from causes — retires channel 40's rolled
  stub); precursor waves with vigor classes and the coarse civ-arc sim
  (rise/peak/decline on the real raster, cause-typed endings, inter-wave
  contact); the four living-residue channels (machine descendants,
  biosphere engineering, sterilization scars, dormant remnants); precursor
  sites as hex-tier anchors via the existing pre-commitment mechanism.
- **Seeding passes 2–4 retired**; pass-1 density/traversability reads the
  simulated fields; homeworld anchors + species profiles derive from
  sapient origins (machine species may descend from precursor capitals).
- **Staggered polity entry** consumes the schedule: entry epoch, homeworld,
  species profile seed, and maturation-quality starting conditions
  (late-emerger contact bonus per the design).

**Boundary**: archaeology/salvage *consumption* and POI compilation are I
(sites exist, nothing digs); native-emergence crises inside claimed space
resolve via native policy in H (record the event, stub the resolution);
tech-tier starting conditions may write `Economy.TechTierStub`-adjacent
state but real tech domains are G; dormant remnants are registry entries,
not encounters. No epoch-sim mechanics changes beyond genesis inputs and
entry-time state.

## Session shape (per /CLAUDE.md)

1. One-message scope confirmation → user nod.
2. Branch `slice-f-deep-genesis` from main; ledger
   `docs/superpowers/plans/YYYY-MM-DD-slice-f-ledger.md`; TDD; frequent
   commits. Don't share a checkout with another live session — take a
   `git worktree` if one exists.
3. Gates: `dotnet test` green (Phase-1 generation suite untouched;
   seeding-pass stub tests may be replaced) · determinism byte-identity
   incl. all new genesis layers · load-vs-rebuild + load-then-continue ·
   genesis cost within budget (cosmic ~1s class, arcs bounded — the
   design docs state budgets) · conservation where the design says
   conserved (the field stack is P4: mass/metals ledger) · shape bands
   (emergence dates spread across the window, homeworld count sane,
   precursor sites bounded).
4. REPL surface: galaxy-side map layers (gas, metallicity, stellar age,
   biosphere, emergence) · a features/precursors dump · the deep-time
   chronicle rendering beneath the epoch chronicle. The eyeball gate:
   **a galaxy whose maps visibly tell its formation story** (a merger
   stream you can point at, metal-rich arms vs a burned-out core, life
   clustered where stability allowed) and **a precursor arc you can read
   end-to-end** (rise, extent, dated fall, ruins where its ports were,
   and a present-day polity entering late because that wave's war
   sterilized its cradle).
5. User gates: scope nod · REPL eyeball · merge decision.
6. Wrap-up: merge · HANDOFF · **write the Slice G kickoff prompt**
   (interior & corporations — it consumes E's commander slots and D's
   niches; read the roadmap row before writing) · flip the box below ·
   push only on user say-so.

- [x] Slice F complete
