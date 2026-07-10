# Session Handoff — 2026-07-09 (Slice B: Two-plane state — merged)

State: `main`, **not pushed** (push on user say-so; main now carries slices
A+C+B). Tests 205/205 green — hex-tier suite untouched at 100%; the drop from
C's 256 is the deleted prototype-sim suite (−82) plus B's new tests (+31).
ProjectSettings churn remains uncommitted as always.

The slice-B/C parallel-session fork (B branched off C's tip) was resolved by
pausing B, merging C, then rebasing B onto main — clean. C's parked worktree
`../StarSystemGeneration-sliceC` can be removed once pushed
(`git worktree remove ../StarSystemGeneration-sliceC`).

## What this session did: Slice B of the epoch-sim rebuild, merged

The state-model rewrite (the one slice with a user-reviewed written plan:
`docs/superpowers/plans/2026-07-09-slice-b-plan.md`). The prototype sim is
**gone** — `EpochSim`, `Sim/*`, `Polity`, `War`, `GalaxyEvent`, the v5
serializer, per-cell political state, their 82 tests — deleted outright per
the greenfield rule; git history is the archive.

- **Two-plane state** (`src/Core/Epoch/`): `SimState(config, skeleton)` holds
  the natural raster plus sparse, hex-addressed, id-ordered registries —
  `Ports`, `Lanes`, `Facilities` (shape only), `Fleets` (stub), `Segments`,
  `Polities` (species + expansion/development treasuries). `RegionCell` slims
  to nature's fields (density, void, chokepoint, lean, metallicity, anchors);
  `SkeletonBuilder.Build` = seeding passes only, identical roll sequence.
- **Territory derived, never stored**: `PortDomains` (service radius 4+4/tier
  hexes, voids never serviced, overlap = contested), `LaneMath` (reach
  18+8/tier, both ends must reach; capacity/speed from tiers).
- **Expansion = port establishment**: Perception surfaces treasury + scored
  candidates (`ColonyValuation`; price signal joins in D) → `GenesisController`
  founds when affordable → Resolution establishes tier-1 ports (collisions in
  actor-id order, losers uncharged; homeworld hexes reserved for emergence) →
  Allocation accrues stub income by budget weights, builds lanes
  nearest-first, raises tiers lowest-first → Interior enters polities
  (homeworld = first port, tier 2) and grows segments logistically.
- **New artifact** (`ArtifactSerializer`): 11 layers, versioned per layer,
  both configs stamped, typed payloads, controllers reattach on load,
  truncation refused. Gates green: byte-identity, load-vs-rebuild, culture
  flip, version refusal; golden frozen
  (`tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt`).
- **Events/channels**: `PortEstablished=301`, `LaneOpened=200`,
  `PortTierRaised=201` (next economic: 202); `RollChannel` 40 = entry
  schedule (next free 41; 37–39 retired).
- **REPL**: `epoch <seed> [epochs] [radiusCells]` · `emap [domains|lanes]`
  (port-domain glows with organic borders; lane webs) · `chronicle [actorId]`
  · `esave`/`eload`. Eyeball-accepted 2026-07-09 after one tuning:
  `HomeworldRatePerCell` 0.02→0.008 (~13 polities at radius 21). Review
  subagent ran; one fix wave (ledger notes).

Ledger: `docs/superpowers/plans/2026-07-09-slice-b-ledger.md` — notes carry
the fork-anomaly record and the surprises (homeworld-hex reservation,
entry-step segment timing).

## Next up

1. **Slice D (Segments & markets)** — fresh session, point it at
   `docs/superpowers/plans/2026-07-09-slice-d-kickoff-prompt.md` (**complete**:
   C half + B half both filled). Between B and D the sim is expansion-only by
   design — a settlement-history sim.
2. **Push** — main carries A+C+B unpushed; push on user say-so.
3. **User read-through of the design specs** — still outstanding.

## Carried process conventions (unchanged)

Lighter protocol per /CLAUDE.md (scope nod · REPL eyeball · merge decision;
kickoff-prompt chaining; B's written-plan exception is spent); hex-tier suite
never breaks; ProjectSettings stays uncommitted; bash printf for REPL piping;
HANDOFF.md is uppercase in git; parallel slices never share a checkout — take
a `git worktree` each. Older carried minors: see
`git show a1f5843~40:docs/HANDOFF.md`.
