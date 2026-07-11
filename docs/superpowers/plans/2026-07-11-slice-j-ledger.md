# Slice J — Handoff & certification

Branch `slice-j-handoff`. Kickoff:
`docs/superpowers/plans/2026-07-11-slice-j-kickoff-prompt.md`. Scope nod:
user confirmed 2026-07-11 — **both deferred wires adopted** (ruins
lawlessness/piracy, memorial stance anchors).

## Architecture decisions (made at survey, before task 1)

- **Wires first, certification after**: J1/J2 change sim behavior (and
  the golden); land them before the resumability/delta work so the
  certification passes run against the final mechanics. Golden refreezes
  once at slice end as always.
- **Ruins lawlessness** (chronicle-and-poi.md live-effects table): ruins
  and dead-city POIs project a lawlessness modifier onto nearby space.
  Wire = the pirate-band founding trigger (CorporationOps.FindNiche):
  today it requires a navyless owner + posted capacity ≥
  RaidCapacityFloor; a lane within reach of a standing ruins POI reads a
  reduced floor (lawlessness × cargo value, as the design comment always
  said) and the band's haven becomes the ruins hex when closer than the
  owner's port. No new phase, no new registry — a founding-condition
  wire.
- **Memorial stance anchors** (same table): a standing Memorial POI
  anchors the victim's stance about the perpetrator — in
  ReputationOps.DecayStances, a (observer=victim, subject=perpetrator)
  pair covered by a live memorial decays toward an anchored negative
  floor instead of 0. Participants ride PoiRecord.ParticipantActorIds;
  memorial cause (famine/suppression) already on the record. Fades when
  the POI fades (one-anchor-per-hex displacement) — nothing persisted
  beyond what already is.
- **Fine tick is a config override, not a new engine**: the same
  EpochEngine steps with a small Sim.YearsPerEpoch over a loaded
  artifact. Loading stamps the artifact's config; the REPL fine-step
  command overrides YearsPerEpoch on the loaded state's config before
  stepping. Certification = shape/invariant tests at YearsPerEpoch ∈
  {25, 5, 1}: determinism byte-identity, LoadThenContinue, conservation
  (credits/hulls/population), no genesis-only mechanic (per-epoch
  constants that should be per-year rates surface here — fixing them is
  the slice).
- **Delta boundary**: a delta save = base-artifact hash + per-registry
  deltas + the log continuation. Implementation: DeltaSerializer over
  the same line grammar — for each layer, emit only lines that differ
  from the base artifact's (line-keyed by registry id; adds and edits;
  registries never shrink — dead records stay as history, so no
  tombstones needed). Load = load base, apply deltas. Round-trip gate:
  base + deltas ≡ full state byte-identically. REPL: `esaved <base>
  <path>` / `eloadd <base> <path>` (names may change at implementation).
- **Handoff framing compiles nothing** (chronicle-and-poi.md): indexes
  and open threads are computed views over the loaded state — a
  HandoffView in Core (open threads: loaded tensions near the war floor,
  pending successions (aged rulers/heirless thrones), half-won wars
  (objectives partially taken / exhaustion mid-band), leveraged
  corporations (wealth near the nationalization line), live plagues,
  quarantines, leaderless realms) + REPL `threads` panel. Per-war /
  per-character indexes largely exist as EventLog views; surface the
  gaps, don't store them.
- **Controller handover**: polity-slot swap test exists (slice I). J
  extends certification to a corporation slot mid-run (byte-compare
  tail) and certifies the swap API itself (Actor.Controller is the whole
  interface; nothing downstream branches on controller type).

## Tasks

- [x] J1 — Ruins lawlessness wire: pirate-band trigger reads standing
      ruins POIs (reduced raid floor near ruins, ruins-haven bands);
      knob(s) in Poi.*; TDD: band founds near ruins that wouldn't
      otherwise; no ruins → old behavior byte-identical.
      Notes: a Ruins/RuinedCapital POI within Poi.LawlessnessReachHexes
      (3) of either lane mouth = lawless lane: raid floor ×
      Poi.LawlessRaidFactor (0.4) AND the navyless requirement waived.
      Golden regen: +2 KNOB lines only — seed 42 history byte-identical.
- [x] J2 — Memorial stance anchors: DecayStances holds any stance that
      reached −Poi.MemorialStanceAnchor (0.25) against a standing
      memorial's perpetrator; suppression memorials carry SubjectId =
      the suppressing polity (famines keep −1 — no foreign author).
      Golden: seed 42 memorials name their perpetrators; stances
      against them persist; small warmth/tension reprice downstream.
- [x] J3 — Fine-tick resumability. Landed in four waves:
      (a) **Sim.GenerationYears** (ESIM, config v6) — the calendar unit
      every *Epochs knob counts; all persisted clocks converted to
      world-years: relations Met/Rung/Offer/LastIncident/VassalSince
      (v5), WarObjective.SiegeYears (v2), Corporation.LeanYears (v2),
      Faction.NichePersistenceYears (interior v6); entry + native
      emergence fire on the calendar; era buckets are generations.
      Golden diff = pure unit rescale, zero event drift.
      (b) Per-generation intensities × Sim.StepFraction: incident
      sparks, battle loss shares (hash-rounded hulls, RollChannel 72),
      facility damage, commander rout-death.
      (c) Regional news spreads by age-crossing (once per observer,
      when age crosses delay; horizon one generation).
      (d) FineTickTests: fine determinism byte-identity, fine
      LoadThenContinue, seven-phase/clock honesty, hull conservation,
      liveness + macro bands over 2 seeds. Bands caught 2 real bugs:
      price signal compared per-step demand flow to inventory stock
      (universal glut at fine tick → floor crash; demand now
      normalizes by StepFraction) and yard slots truncated fractional
      throughput (0 hulls forever at fine; hash-round, RollChannel
      73). Coarse behavior byte-identical throughout. REPL:
      `estep [n] [years]`.
- [ ] J4 — Controller handover certification: scripted controller takes
      a corporation slot mid-run (polity already certified);
      byte-compare the untouched remainder; assert no sim code reads
      controller identity.
- [ ] J5 — Delta boundary: DeltaSerializer (save/load against a base
      artifact), round-trip byte-identity gate (base + deltas + log ≡
      live state), REPL delta save/load pair.
- [ ] J6 — Handoff framing: HandoffView open-threads computation +
      REPL `threads` panel ("the world in motion"); index-view gaps
      filled where the design names them (per-war view).
- [ ] J7 — Full-design acceptance pass: sweep docs/design/ vs
      implementation; every P-number certified or gap filed; remaining
      perfect-info/stub comments hunted; design amendments in-branch;
      certification record committed
      (docs/superpowers/specs/2026-07-XX-design-acceptance.md).
- [ ] J8 — Gates + review: full suite green (hex tier untouched),
      determinism byte-identity at both resolutions, golden refrozen
      once, fresh-eyes whole-branch review + one fix wave.
- [ ] J9 — Docs/diagram sync: design-tree amendments merged;
      generation-flow diagram updated + republished to the existing
      artifact URL.
- [ ] J10 — Eyeball gate: load a finished epoch run, step at fine tick,
      watch the same war keep burning; take a polity's controller slot;
      quarantine a lane by hand; `threads` panel readout. Then merge
      decision, HANDOFF, Slice K kickoff prompt.

## Log

- 2026-07-11: scope nod (both wires adopted), branch cut, survey
  complete, ledger committed.
