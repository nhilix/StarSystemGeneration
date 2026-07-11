# Slice I — Narrative (perception, news, reputation, chronicle, POI, plagues)

Branch `slice-i-narrative`. Kickoff:
`docs/superpowers/plans/2026-07-11-slice-i-kickoff-prompt.md`. Scope nod:
user said "read the kickoff and begin" (2026-07-11) — treated as the nod;
scope restated in-session.

## Architecture decisions (made at survey, before task 1)

- **One staleness mechanism**: a per-(observer, subject) belief snapshot,
  refreshed when the traffic-derived news delay allows. Self-facts (own
  credits, ports, designs, expansion, own gauges warmth/tension) stay
  fresh; other-side facts (strengths, defensive strengths, casus-belli
  menu, objective candidates, war exhaustion/strength-share) snapshot and
  go stale. The snapshot IS the stale value — no truth history kept.
- **News delay** = multi-source Dijkstra over the port graph from an
  observer's own ports; lane traversal years = hexdist / newsSpeed(lane),
  newsSpeed = f(TrafficPerYear) (busy fast, backwater slow); off-lane
  crossings at a wilds crawl. Computed per epoch from live fleet state —
  never persisted (derivable).
- **Belief serializes** (H lesson: LoadThenContinue) as a new `belief`
  layer: per-actor stance table + per-pair PolityBelief + per-war
  WarBelief — all compact int/double lines.
- **News pulses**: Chronicle emits for Public events over a magnitude
  floor; a pulse is (event id, origin, emit year, delivered-actor set);
  Perception delivers when age ≥ delay(observer ← origin); arrival (not
  emission) refreshes belief + moves stances; pulses expire at MaxAge.
  Serialized (`pulses` in the belief layer or its own).
- **Reputation is derived**: stored per-actor stance valences [−1,1] per
  known actor, updated at news arrival through the observer's temperament
  (militants respect conquest, open traders sanction treaty-breakers),
  decaying toward 0. Feeds WarmthTarget as a new term (pair-mean stance)
  — one wire reprices first contact, treaty gates, war appetite.
  NationalizeLegitimacyHit stays domestic; the news now also moves
  foreign stances.
- **Era detection** is a pure view over the EventLog (recomputable
  annotation, never sim state): epochs clustered by dominant event
  signature, named from participants.
- **POI compiler** runs inside Chronicle each epoch: battlefields
  (wreckage concentration ≥ floor), ruins (razed facilities / dead
  cities), ruined capitals (capital captures), memorials (famines,
  annihilations), precursor sites surfaced from the skeleton registry.
  One anchor per hex by magnitude. WreckageRecords stay immutable —
  depletion tracked on the POI (HullsSalvaged), so the hull-ledger
  invariant (Σ wreckage == Σ HullsWrecked) survives salvage.
- **Salvage niche**: corporations charter into battlefield/precursor
  POIs; draws convert POI salvage into alloys/components at the local
  market, decrement the POI, mint Explorer notables on first dig.
- **Plagues**: registry of outbreaks (RollChannel 70+); spread along
  lanes gated by TrafficPerYear; segment mortality skips machine
  embodiment; wealth never minted (deaths shrink Size, wealth stays as
  inheritance); burnout + port immunity windows; QuarantineAct resolves
  to a lane closure that stops freight, migration, spread, and news.
- Event ids: economic 208+ (plague), political 310+ (quarantine),
  military 408+, diplomatic 511+ (reputation shock?), character 704+
  (expedition). RollChannel next free: 70.

## Tasks

- [x] I1 — News graph: NewsOps.DelayYears (traffic-derived speeds,
      off-lane crawl), knobs, unit tests (busy < backwater < wilds).
- [x] I2 — Belief store: BeliefState per actor; Perception splits
      fresh self-facts from snapshot other-facts; `belief` artifact
      layer; staleness-varies-with-traffic + LoadThenContinue tests.
      Notes: DefensiveStrength/ObjectiveCandidates moved from
      PerceptionPhase into BeliefOps (snapshot at refresh time);
      WarBrief front reports route from the opponent leader's capital;
      CorporateBrief credits go stale by HQ distance; ColonyValuation
      deliberately left as the capital's own view (it surveys nature,
      which doesn't move — nothing to stale). Golden regenerated
      (belief layer + behavior change); refreezes at slice end.
- [x] I3 — News pulses: emission at Chronicle, delivery at Perception,
      serialization, expiry; arrival-not-emission test.
      Notes: pulse journeys (per-polity arrival years) live on the pulse
      record — the REPL news panel reads them; NO NewsArrived log events
      (would multiply the log by the polity count). Delivery also
      force-refreshes any existing belief about involved polities when
      the word is newer than the snapshot.
- [x] I4 — Stances + reputation: stance table, temperament-filtered
      updates per conduct event family, decay; WarmthTarget reputation
      term; FirstContact seeds pre-heard stances; tests (breaker meets
      colder; militant vs open audiences diverge).
      Notes: per-event stance deltas + tilts are structural constants in
      ReputationOps.Judge (like the stance buckets); dials are
      News.StanceDecayPerYear and Relations.ReputationWarmthWeight.
      Dogma filter = negative deltas amplify with ideology gap.
      LastWarmthTerms grew to 6 (reputation) — REPL labels updated.
      Regional events spread by contact: stateless log-tail scan, judged
      by whoever is within one epoch's news delay. FirstContact seeding
      is automatic (Contact seeds at WarmthTarget, which now reads
      stances heard before the pair ever met).
- [ ] I5 — War staleness teeth: WarBriefs through belief; shape test —
      slow news concedes later than fast news (fog of war).
- [ ] I6 — Chronicle views + era detection: EraDetector over the log;
      REPL `eras`, era-annotated `chronicle`, per-place/per-actor views.
- [ ] I7 — POI compiler: PoiRecord registry + Chronicle compiler +
      `pois` layer + REPL `poi`; POIs-accumulate-where-history-happened
      shape test.
- [ ] I8 — Salvage + expeditions: corporate salvage niche, conserving
      draws, Explorer notable, precursor digging; hull-ledger test.
- [ ] I9 — Plagues: PlagueOps + QuarantineAct resolution + immunity/
      burnout + events + `emap plague`; conservation + burnout tests.
- [ ] I10 — REPL polish: `belief`, `news`, `stances` panels; chronicle
      prose for every new event; `watch` intact.
- [ ] I11 — Gates: full suite, determinism byte-identity, golden regen
      (once, at slice end), fresh-eyes review + one fix wave.
- [ ] I12 — Eyeball gate: the fog of war made visible (distant loser
      fights on; reputation shock travels; battlefield POI dug).

## Log

- 2026-07-11: branch cut, survey complete, ledger committed.
