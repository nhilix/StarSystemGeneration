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
- [x] J4 — Controller handover certification (HandoverTests): every
      controller wrapped in a delegating driver mid-run → byte-identical
      continuation; scripted player takes a polity throne (pinned tax
      code + TreatyAct resolving by the same ladder rules) and a
      corporation board (dividend policy) mid-run; load reattaches
      stock controllers (occupation is client state).
      NOTE: no such byte-compare test existed before J despite the
      kickoff's claim — it does now.
- [x] J5 — Delta boundary: DeltaSerializer over the artifact's own line
      grammar — layer-granular diff, events layer ships only its
      continuation, unchanged layers absent (genesis strata never
      re-record), FNV-64 base fingerprint refuses foreign bases.
      Round-trip byte-identity certified at both tick resolutions.
      REPL `edsave <base> <delta>` / `edload` (smoke: 100y fine
      continuation = 57KB delta on a 107KB base).
- [x] J6 — Handoff framing: HandoffView.OpenThreads (half-won wars,
      loaded tensions with live casus belli, old thrones by species
      span + standing succession claims, leveraged corporations,
      burning plagues, quarantines, unanswered offers) + REPL `threads`
      panel; EventLog.ForWar via IWarPayload completes the four
      chronicle indexes. Seed 42 hands off the Alloys War at 3/4
      objectives, 7 pairs at the brink, 6 old thrones, 2 plagues.
- [x] J7 — Full-design acceptance pass:
      docs/superpowers/specs/2026-07-11-design-acceptance.md — P1–P8
      certified with evidence (P2 partial: character personal acts
      unarmed; P3 one filed exception: true-price freight), per-plane
      verdicts, 13-item consolidated gap list (headline: 11 contract
      acts are type-only — the player-verb backlog). Six files'
      stale perfect-info/stub comments fixed; perception-and-news.md
      amended to the landed per-subject snapshot model.
- [x] J8 — Gates + review: 562/562 green (hex tier untouched);
      determinism byte-identity certified at both resolutions; golden
      frozen at the final format (J3a regen; byte-audited by the
      reviewer — all changes are unit rescales + the J2 wire).
      Fresh-eyes review: 1 CONFIRMED (corporation Receipts is a
      per-step flow vs per-generation floors — corps mass-died and
      magnates stopped minting at fine tick; both comparisons now
      normalize by StepFraction), 2 plausible (regional-news
      horizon-straddle window served + delay-churn double/skip
      documented as accepted; DeltaSerializer refuses a section for a
      layer the base lacks), 3 notes (RoundLoss key stride 65536;
      SimTraceView entry dates on GenerationYears; dead local) — one
      fix wave, all addressed.
- [x] J9 — Docs/diagram sync: perception-and-news.md amended (J7);
      generation-flow.html synced to slices A–J (game-layer
      foundations DONE, REPL J row, world-year clocks, certification
      language). Artifact republish to the existing URL happens after
      the merge decision (living-baseline procedure).
- [ ] J10 — Eyeball gate (USER): exhibits prepared and dry-run —
      `equarantine` added as the hand-issued polity verb. Then merge
      decision, HANDOFF, Slice K kickoff prompt.

## Log

- 2026-07-11: scope nod (both wires adopted), branch cut, survey
  complete, ledger committed.
