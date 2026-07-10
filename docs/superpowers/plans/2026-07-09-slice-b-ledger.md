# Slice B Ledger — Two-Plane State (slice-b-two-plane-state)

Ordered task checklist per the reviewed plan
(`2026-07-09-slice-b-plan.md`). Updated and committed as tasks complete —
this is the resumability record. New state code lives in `src/Core/Epoch/`;
the prototype sim is deleted in Task 5.

## Tasks

- [x] 0. **Branch + ledger** — branch `slice-b-two-plane-state`; this file.
      **⚠ Fork anomaly**: the branch forked from in-flight
      `slice-c-substrate-catalogs` (at 65446a5, C tasks 1–6), not main — the
      working tree was on C's branch at checkout. **User decision (paused
      after task 4): finish slice C first** (review · eyeball · merge to
      main), then resume B with:
      `git rebase --onto main 65446a5 slice-b-two-plane-state`
      — replays f52f522..HEAD (docs + tasks 1–4) onto post-C main. After the
      rebase, note for task 5: C's `infra` REPL command reads
      `RegionCell.DevelopmentTier`/`PopulationSpeciesId` — task 5 deletes
      those fields and must adapt that command (neutral wilds inputs).
- [x] 1. **Config knobs, roll channel, event types** — `InfrastructureKnobs`,
      `ExpansionKnobs`; `RollChannel.EpochEntrySchedule = 40`;
      `WorldEventType.{LaneOpened=200, PortTierRaised=201, PortEstablished=301}`
      + payloads. Gate: solution green.
- [x] 2. **Registry entry types** — Port, Lane, Facility, FleetRecord,
      PopulationSegment, PolityRecord (+ `.meta`s). Gate: structural tests.
- [x] 3. **Derived geography** — `PortDomains` (service radius, servicing,
      owners-at, contested), `LaneMath` (range, capacity, speed). Territory
      computed, never stored. Gate: PortDomainTests green.
- [x] 4. **EpochGenesis** — seed polities from homeworld anchors;
      `SimState(config, skeleton)` + registries; homeworld = first port
      (tier 2) at entry in Interior; `SkeletonBuilder.BuildNatural`; delete
      `StubGenesis` (channels 37–39 retired). Gate: EpochGenesisTests green,
      solution green (267/267). REPL smoke: `epoch 42 8 10` → 7 polities
      from real anchors, staggered entry.
- [x] 5. **Prototype deletion + raster slim** — deleted `EpochSim`, `Sim/*`,
      `Polity`, `War`, `GalaxyEvent`, `SkeletonSerializer`, Inspector
      political surface, nine prototype test files (82 tests); slimmed
      `RegionCell`, `RegionContext`, `GalaxySkeleton`, `GalaxyConfig`,
      `PassHomeworlds` (roll sequence unchanged). C's `infra` command adapted
      to neutral-wilds inputs, workforce from the homeworld anchor.
      Gate: green (186), hex-tier intact, Inspector builds. **Resumed here
      after the C merge**: rebased onto main via the recorded command, clean.
- [x] 6. **Allocation stub income** — per-port world-year income × standing
      budget weights → polity treasuries; `Actor.Policies` written in Intent.
- [x] 7. **Expansion chain** — `ColonyValuation`, `PerceptionView` extension,
      `GenesisController`, `FoundColonyAct` resolver. Surprise: colony
      targets could squat unentered polities' homeworld anchors → duplicate
      port at entry; homeworld anchor hexes are now reserved for their
      species' emergence.
- [x] 8. **Lanes + tier growth** — lanes nearest-first then tier raises
      lowest-first from development points; `LaneOpened`/`PortTierRaised`.
- [x] 9. **Segment growth** — logistic per world-year toward port-tier caps;
      entry-step segments start integrating the following epoch.
- [x] 10. **Artifact format** — 11 layers, versioned per layer, both configs
      stamped; typed payload round-trip; controllers reattach on load;
      `esave`/`eload`. Gates all green (byte-identity, load-vs-rebuild,
      culture-flip, version refusal).
- [x] 11. **REPL surface** — `emap [domains|lanes]`, `chronicle [actorId]`,
      trace registry summary; piped smoke OK. Test arithmetic:
      268 (post-C main) − 82 prototype + 17 new = 203/203 green.
- [ ] 12. **Gates + wrap-up** — full test run ✓ (205) · fresh-eyes branch
      review subagent + one fix wave ✓ (no blockers; truncation refusal,
      IO hardening, dead knobs, shared hex rounding) · **USER: REPL eyeball
      ✓ accepted 2026-07-09** after tuning HomeworldRatePerCell 0.02→0.008
      (~13 polities — map legibility) · goldens frozen ✓
      (`Goldens/slice-b-artifact-seed42.txt`, radius 12, GoldenTests) ·
      **USER: merge decision** (pending) · HANDOFF · Slice D kickoff
      prompt · kickoff checkbox.

## Notes / surprises

(fill as encountered)
