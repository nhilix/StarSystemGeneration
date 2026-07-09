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
- [ ] 1. **Config knobs, roll channel, event types** — `InfrastructureKnobs`,
      `ExpansionKnobs`; `RollChannel.EpochEntrySchedule = 40`;
      `WorldEventType.{LaneOpened=200, PortTierRaised=201, PortEstablished=301}`
      + payloads. Gate: solution green.
- [ ] 2. **Registry entry types** — Port, Lane, Facility, FleetRecord,
      PopulationSegment, PolityRecord (+ `.meta`s). Gate: structural tests.
- [ ] 3. **Derived geography** — `PortDomains` (service radius, servicing,
      owners-at, contested), `LaneMath` (range, capacity, speed). Territory
      computed, never stored. Gate: PortDomainTests green.
- [x] 4. **EpochGenesis** — seed polities from homeworld anchors;
      `SimState(config, skeleton)` + registries; homeworld = first port
      (tier 2) at entry in Interior; `SkeletonBuilder.BuildNatural`; delete
      `StubGenesis` (channels 37–39 retired). Gate: EpochGenesisTests green,
      solution green (267/267). REPL smoke: `epoch 42 8 10` → 7 polities
      from real anchors, staggered entry.
- [ ] 5. **Prototype deletion + raster slim** — delete `EpochSim`, `Sim/*`,
      `Polity`, `War`, `GalaxyEvent`, `SkeletonSerializer`, Inspector
      political surface, nine prototype test files; slim `RegionCell`,
      `RegionContext`, `GalaxySkeleton`, `GalaxyConfig`, `PassHomeworlds`.
      Gate: solution green, hex-tier suite intact, Inspector builds.
- [ ] 6. **Allocation stub income** — per-port world-year income × standing
      budget weights → polity treasuries; `Actor.Policies` written in Intent.
      Gate: AllocationTests green.
- [ ] 7. **Expansion chain** — `ColonyValuation`, `PerceptionView` extension,
      `GenesisController`, `FoundColonyAct` resolver (tier-1 ports, collision
      order, `PortEstablished` events). Gate: ExpansionTests green.
- [ ] 8. **Lanes + tier growth** — Allocation builds in-range same-owner
      lanes (dist order) and raises lowest-tier ports from development
      points; `LaneOpened`/`PortTierRaised` events. Gate: determinism asserts.
- [ ] 9. **Segment growth** — Interior integrates logistic growth toward
      port-tier caps. Gate: InteriorTests green.
- [ ] 10. **Artifact format** — `ArtifactSerializer`: layer-sectioned,
      versioned per layer, both configs stamped; `esave`/`eload`. Gates:
      byte-identity across runs, load-vs-rebuild equivalence, culture-flip,
      version refusal.
- [ ] 11. **REPL surface** — `emap [domains|lanes]`, `chronicle [actorId]`,
      trace registry summary; piped smoke
      (`printf 'epoch 42\nemap\nemap lanes\nchronicle\nquit\n' | dotnet run --project src/Inspector`).
- [ ] 12. **Gates + wrap-up** — full test run · fresh-eyes branch review
      subagent + one fix wave · **USER: REPL eyeball** (domain glows,
      organic borders, lane webs, founding chronicle) · goldens frozen
      (seed 42, radius 12) · **USER: merge decision** · HANDOFF · Slice D
      kickoff prompt · kickoff checkbox.

## Notes / surprises

(fill as encountered)
