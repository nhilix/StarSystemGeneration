# Domain hex expansion — a starport's region comes alive — design (2026-07-16)

Status: approved (brainstormed with the user 2026-07-16), ready for a
writing-plans pass. One mega-slice, three independently-mergeable phase
gates, the Slice L pattern.

## Motivation

A starport today administers a service radius spanning many hexes
(`PortDomains.ServiceRadius`, `frame/space-and-travel.md` §"Ports and
domains"), but almost nothing happens across that radius. Facility siting
scans the domain's cells yet collapses every choice onto one anchor hex per
cell (`CapabilityOps.PickHex`), population lives as a single aggregate at the
port body (`PopulationSegment` carries `PortId` + a `Body` that
`PopulationSiting.Assign` always resolves to `BodySiting.PortBody`), and the
only way a region grows a *second* settled hex is a polity-scale colonization
convoy leaping to a fresh system. A domain is drawn as a glow but simulated as
a point.

This is the **adjacent-hex spillover** thread — Slice L follow-up #1, raised by
the user mid-slice when a body-poor hex's bodies were all claimed and no
facility could expand past it (`docs/HANDOFF.md` §"Filed as follow-ups"; Slice
L ledger, L2 ledger). It has been deferred three times — each time because the
narrow framing ("let a facility overflow to the next hex when this one is
full") changes `Facility.Hex` semantics and touches the separate `Siting.cs`
ranking module, and never justified its own brainstorm. This design stops
narrowing it. The real want is not overflow plumbing; it is a **domain that
fills in and reaches out** — corporations and segments settling the domain's
interior hex by hex on their own books, while the polity's treasury still
decides the large moves. Scarcity-driven overflow becomes one case of a
general, always-on opportunity siting.

Two scales stay sharply separated, and keeping them separate is the whole
design:

- **Single-hex expansion is local and self-funded.** A corporation builds a
  satellite working at a rich neighbor hex out of its own credits; a
  population segment elects to settle a worked hex out of its own wealth.
  These need no treasury vote.
- **Starports remain polity-scale treasury investments.** A colonization
  expedition is a *reach* leap to a fresh system, charged from
  `ExpansionPoints` (`Phases.cs` expedition dispatch,
  `ProjectOps.CompleteExpedition`). Graduating a mature frontier outpost into
  a starport is *infill* — the complementary investment mode, weighed against
  the same treasury, the same scoring.

The three-rung ladder is: **satellite workings → outpost → frontier-gated
starport candidacy.** Each rung is a phase gate of the slice.

## §1 Overview

A starport's domain becomes a living region through three rungs, each a
distinct actor-scale and funding source:

1. **Satellite workings** (§2) — corporations and the polity site *individual
   facilities* at the best hexes across the whole domain, not just the port
   hex, scored on real body richness. Self-funded by the builder. No new
   persisted state.
2. **Outposts** (§3) — sustained unmet labor at a worked hex triggers a
   population segment to *elect to settle* there, spending its own wealth; the
   settlement event founds a lightweight **outpost** record. Pop follows work.
3. **Frontier graduation** (§4) — a mature outpost far enough from every
   existing port becomes a **starport-candidacy target** in the same polity
   expansion scoring as an expedition, charged from `ExpansionPoints`. Infill,
   not reach; no convoy.

The explicit anti-goal, fixed by the user: **starports must never end up
founding adjacent to each other.** Infill graduation is **densification** — an
outpost can graduate at the fringe of its parent's domain, *inside* it — so the
gate is not "outside every domain" but a hard **anti-adjacency spacing** (§4)
derived from the tier-1 service radius: no two port cores ever sit within a
newcomer port's own reach of each other. Spacing scales with config, never an
absolute constant. (This is categorically distinct from the expedition's
gate-range *reach leap*, which founds a fresh colony far away and is the mechanic
`EncroachedPolities` governs — graduation must not borrow that geometry.)

This is a **sim-only slice**. The REPL is the eyeball surface (§6). The atlas
rendering of blooming domains, outposts, and graduation is explicitly deferred
to the future atlas pass (§8, Forward roadmap).

## §2 Stage 1 — Satellite workings: hex-granular opportunity siting

Candidate generation drops from cell granularity to **hex granularity** across
the port's service radius.

### The change to `CapabilityOps.ConstructionCandidatesFor`

Today the scan (`CapabilityOps.cs` `ConstructionCandidatesFor`, ~lines 75–140)
iterates `state.Skeleton.Cells` in spiral order, and for each cell within
`PortDomains.ServiceRadius(cfg, port.Tier) + TechOps.AstroRadiusBonus`, scores
each buildable type once at the cell and calls `PickHex` to collapse the whole
cell onto a single anchor hex. The scan becomes **per hex**: for every hex the
port services (deterministic spiral over the domain's hexes — see determinism
below), a **body-aware opportunity score** per facility type.

The score has two regimes, split by whether the hex is already settled:

- **Unsettled hexes** get a deterministic on-demand **preview**: the hex-tier
  generator is run at scan time and *discarded*. This is exactly
  `SystemQuery.At`'s unsettled branch — `Generator.Generate(context, hex).System`
  (`src/Core/Atlas/SystemQuery.cs:69-71`) — called **without**
  `SystemRegistry.Commit`. Nothing is persisted; the hex stays pristine by
  definition, and the preview is a pure function of `(GalaxyConfig, hex)`, so
  it is repeatable and roll-free.
- **Settled hexes** read the frozen `state.SettledSystems[hex]` record, real
  `state.BodyResources` depletion, and the per-hex per-class body claims that
  `ProjectOps.PlaceFacilityBody` already collects — no re-derivation.

### The opportunity score

For **extraction types** (Mine, Skimmer, AgriComplex, ExcavationSite —
`BodySiting.IsExtraction`), the score is the richness/stock of the best
*eligible unclaimed* body the type could take at that hex, run through the same
body machinery the sim already uses:

- Depletable types (Mine, ExcavationSite) score on the remaining
  `BodyResources` stock of the body `BodySiting.Assign` would pick (or, for an
  unsettled preview, the stock `BodyResourceOps.Commit` *would* roll — computed
  from the region's raster potential, so no roll is needed to preview it).
- Renewable types (Skimmer, AgriComplex) score on `BodySiting.RenewableYield`
  of the body they would claim.
- A hex whose only eligible body is already claimed by a competitor
  (`BodySiting.CompetesForBody`) scores that type at zero there — the general
  form of the overflow case that started this thread: a full hex simply loses
  to a neighbor with a free body, at every siting decision, automatically.

The raw richness is then **discounted by distance**: the existing staffing
falloff shape (`StaffingOps.ProximityWeight` uses
`1 / (1 + StaffingDistanceFalloff * dist)`) plus a **hauling proxy** — a
distance term standing in for the cost of moving output back to the port
market. (Its exact form is an implementation-plan choice, §"Open
implementation choices"; the design fixes only that farther hexes are worth
less, so the port hex and its near neighbors keep an advantage that scarcity
must overcome.)

For **support and processing types** (everything in
`CapabilityOps.BuildableTypes` that is not extraction — Refinery, Fabricator,
Foundry, ComputeCore, Fortress, …), the score keeps **port-body affinity**:
these cluster where people and markets already are, so they are scored at or
near the port hex and fall off fast with distance. This preserves the port
hex's gravitational pull — the domain blooms with extraction at its rich
frontier while its industrial core stays anchored at the port.

### Anchors demote to a bonus

`PickHex`'s current job is *selection*: it returns the first free non-homeworld
anchor hex in a cell, else the cell center, and that becomes the facility site.
With per-hex scanning, the hex is no longer chosen by `PickHex` — it *is* the
scan unit. An anchor at a hex becomes a **score bonus** for that hex (the way
`ColonyValuation.CandidatesFor` already adds `0.4` for a non-homeworld anchor),
not the selector. `PickHex` as a site-picker is retired from this path.

### Corporations run the same domain scan

Today a corporation does **not** scan a domain at all: `Phases.cs` (~lines
153–169) synthesizes a single `ConstructionCandidate` from
`CorporationOps.PlannedFacility` at the corp's *home port hex only*. This
design routes corps through the same hex-granular domain scan, scoped to their
home port's domain (`corp.HomePortId`).

**The owner-filter seam.** `ConstructionCandidatesFor` filters ports by
`port.OwnerActorId == actorId`. Ports are owned by polities; a corporation's
actor id never equals a port's owner (a corp's home port belongs to its
`HostPolityId`). So naively passing a corp actor id to the current method
scans **zero ports** and returns an empty list. The scan must be scoped by the
corp's **home-port domain** (the port whose id is `corp.HomePortId`), not by
owner identity. This is corrected and regression-tested in Stage 1 —
flagged here as **verify at implementation; the naive reuse is a latent bug**
the moment corps are pointed at this method.

### Consumed unchanged

`ProjectOps.SpawnFacilityConstruction` and its `PlaceFacilityBody` helper are
consumed exactly as built: groundbreaking is still the commit trigger
(`SystemRegistry.Commit`), body assignment is still claim-aware per resource
class (`BodySiting.CompetesForBody`), the depletable stock roll is still lazy
and idempotent (`BodyResourceOps.Commit`), and **groundbreaking still rejects
outright** when an extraction type resolves `BodyRef.None`
(`SpawnFacilityConstruction` returns `null`). A hex that previewed well but
generates no eligible body simply fails to build there — the existing Phase-2
rejection semantics, now exercised across the whole domain instead of one hex
per cell.

Wages at this stage still flow to the port-hex households exactly as today
(`MarketEngine.PayWages` is untouched by Stage 1) — a satellite working is
crewed by commute, weighted by `StaffingOps.ProximityWeight`, which already
prices the hex-hop from the port. The wage *redirect* waits for Stage 2, when
someone actually lives at the working.

**Determinism (Stage 1):** fixed hex iteration order — a deterministic spiral
over the domain's hexes, the hex-scale analog of the cell spiral the scan uses
today (P6). Previews are pure functions of seed; no `RollChannel` is consumed
(consistent with the reserved `RollChannel.SimExpansion` note: "stage-1
expansion is roll-free"). **No new persisted state** exists after Stage 1 —
every satellite working is an ordinary `Facility` row with a real `Body`, and
`SettledSystems`/`BodyResources` grow only through the existing groundbreaking
commit.

## §3 Stage 2 — Outposts: pop follows work

Stage 1 scatters facilities across a domain but leaves everyone living at the
port. Stage 2 lets population **follow the work**: a segment elects to settle a
worked hex, and that settlement event *is* the outpost founding.

### Segment gains a hex

`PopulationSegment` gains a `Hex` field (`HexCoordinate`), defaulting to its
administering port's hex. It is serialized (segments layer bump — §5). The
segment keeps its `PortId` (get-only today) as its **administering domain**: an
outpost's residents are still administered by, and trade through, the parent
port. Nothing in Stage 1–2 changes a segment's `PortId`; the single sanctioned
re-attachment is Stage 3's graduation (§4), and *how* that mutation happens
(a settable field vs. row replacement) is an implementation-plan choice. `PopulationSegment.Body` already exists (from the locality slice); Stage
2 lets a segment's `(Hex, Body)` be a satellite hex within the domain rather
than always the port body.

### The settle election

When a satellite hex shows **sustained unmet weighted-labor demand** — a
worked hex whose facilities want more labor than the commute from the port can
supply, persisting for a **world-time duration** (measured in world-years,
never step counts — the hard project rule; the same discipline the settle
clock shares with every other duration in the sim) — an eligible segment
**elects to relocate**:

- It pays a **real habitat cost**: segment `Wealth` is spent as construction
  wages for the habitat, conserved (money leaves `Wealth`, lands as wages in
  the same flow the sim already conserves — verified at sweep scale, §5).
- It moves its `Hex`/`Body` to the satellite hex. `PopulationSiting` is
  extended to resolve a body *within a named satellite hex's system* (it
  resolves within the port's system today; the extension resolves within an
  arbitrary domain hex's committed system).
- **That settlement event founds the outpost.**

Whether the election reuses/extends the existing intra-galaxy migration
machinery or gets its own dedicated pass is an implementation-plan choice
(§"Open implementation choices"), not decided here. What is fixed: the trigger
is sustained unmet labor in world-time, the cost is real segment wealth, and
the founding is the settlement.

### The outpost record

An **outpost** is a lightweight registry record: `(id, name, hex, parent port
id, founding year)`, held in a new `SimState.Outposts` registry. It exists for
fiction, REPL, and metrics. It is **not an actor**: no treasury, no market, no
service radius, no controller. Outposts trade through the **parent port's
market** — no new order books, no second `Market`. The founding event surfaces
in the narrative/news feed (a `StagedEvent`, the mechanism
`CompleteExpedition` already uses for `PortEstablished`).

### Staffing and wage rewire

Two conservation-adjacent rewires, both scoped to Stage 2:

- **Staffing** (`StaffingOps.ProximityWeight`): the hex-hop currently measures
  `port-hex → facility-hex` (`state.Ports[seg.PortId].Hex` to `f.Hex`). It
  becomes `segment-hex → facility-hex` (`seg.Hex` to `f.Hex`), a **local hop**
  (the existing `OrbitGeometry.OrbitDistance` term) when they are the same hex.
  A resident of an outpost now crews its facilities at full weight, while the
  port's distant households crew them weakly — the labor actually localizes.
- **Wage redirect** (`MarketEngine.PayWages`): a satellite facility's wages pay
  the **resident segments at that hex**, not the port-hex households, once
  residents exist. This is conservation-sensitive (it moves where credits
  land, not how many) and is verified at sweep scale (§5). A working with no
  resident yet still pays the commuting port households, unchanged from Stage 1.

## §4 Stage 3 — Frontier graduation: infill

A mature outpost far enough from every existing port becomes a candidate to be
promoted into a real starport — the polity's **infill** investment mode,
complementary to the expedition's **reach**.

### The frontier gate — the anti-clustering guarantee

An outpost is **candidacy-eligible only when it sits at least `G` from every
existing port *core*,** where `G = PortDomains.ServiceRadius(cfg, 1) +
Expansion.GraduationMarginHexes` — the **newcomer's own (tier-1) reach** plus a
configured margin, never an absolute constant. This is a pure **anti-adjacency**
spacing: it guarantees no two port cores ever fall within a tier-1 port's reach
of each other, so a graduated port can never land **adjacent** to an existing
one — the anti-goal, structurally impossible at any config (spacing scales with
the tier-1 service radius).

It is deliberately **NOT** the expedition's `EncroachedPolities` geometry (the
*sum* of both ports' radii). Graduation is **densification, not a reach leap**,
so it must not inherit the leap's "stay outside every existing domain" rule.
A graduating port may sit **inside** an existing, larger domain — its own
parent's, or a foreign polity's — as long as it clears `G` from that domain's
*core*. Founding inside a foreign domain is **allowed** and fires the same
encroachment-tension bump an expedition does (the diplomacy is *priced* by the
tension layer, not *forbidden* by the gate).

**Interior outposts — those within `G` of a port core — never graduate.** They
are permanently subordinate density: worked hexes with residents that stay under
their parent's administration. Only an outpost at the **fringe of its parent's
domain** (far from the parent core, yet still served by it) reaches port scale —
the densifying *second center* of a large domain. A small tier-1 domain, whose
entire radius lies within `G`, cannot densify until its port is raised
(`PortRaise`) and the domain grows — correct and intended (the domain has
interior *and* frontier; only the frontier — the far reach of a big domain —
reaches port scale).

### The promotion

An eligible frontier outpost enters the **same polity expansion scoring** as an
expedition target. The polity weighs infill (promote this outpost) against
reach (`ColonyValuation.CandidatesFor` targets) against its treasury, all in
one scheduler — the existing expansion-decision path, with graduation as a new
candidate kind.

- **Cost** = `Expansion.ColonyCost` **discounted** by the outpost's existing
  facilities and resident population (it is already half a colony — the sim
  should not charge full price for infrastructure that exists). Charged from
  `ExpansionPoints`, the same treasury an expedition draws
  (`record.ExpansionPoints -= cfg.Expansion.ColonyCost` at dispatch today).
- **No convoy.** Founding runs as an **administrative promotion project** with a
  real world-time duration — a `Project` that completes in-place, no
  `FleetPosture.Expedition`, no off-lane crossing, no fuel burn. There is
  nothing to ship; the people and works are already there.

On completion, promotion mirrors `CompleteExpedition`'s founding body:

- A new **tier-1 `Port` + `Market`** at the outpost hex (`state.Ports.Add`,
  `state.Markets.Add`).
- Resident segments **re-attach** to the new port (their `PortId` becomes the
  new port's id — the segments already live at the hex; only their
  administering domain changes).
- Facilities **re-resolve market attachment** through the existing nearest-port
  lookup (`MarketEngine.AttachedMarketIndex`) — the new closer port naturally
  captures the outpost's own works.
- The outpost record is marked **Graduated** (it stays in the registry as
  fiction/history, no longer a candidate).
- **Encroachment-tension checks reuse `CompleteExpedition`'s** neighbor-tension
  bump loop (`Relations.EncroachmentTensionBump`) — a new starport, however it
  was born, provokes the same border friction.

Same polity, same currency — **zero CU interplay.** Graduation moves an
outpost from subordinate to port within one polity's own territory; no
cross-currency, no FX, nothing the currency slice touches.

## §5 State, determinism, conservation

New persisted state is deliberately tiny; everything else is derived.

- **`PopulationSegment.Hex`** — one field. Bumps the `segments` serializer
  layer from v3 to v4 (`ArtifactSerializer.Layers`; append/version discipline —
  a field on an existing layer is a version bump, not a new layer).
- **`SimState.Outposts`** — a new registry. Appends a new `outposts` layer to
  `ArtifactSerializer.Layers` after `banks` (new layers append, never reorder).
  Iterated in id order for any serialized or diagnostic output (P6).
- Everything else — satellite workings, body claims, stocks, port promotions —
  is ordinary existing state (`Facility`, `BodyResources`, `Port`, `Market`).

**New roll needs** (outpost naming, settle-election tiebreaks) take **fresh
`RollChannel` values** appended after the last live channel (`RollChannel`'s
last assigned value is `78`/`ShipmentDetection`, from slice L2; `79+` are
free). This is *verified free in-branch*, not assumed — Stage 1 stays roll-free (previews are
pure), and any Stage 2/3 channel is a genuinely new keyed roll `(step, actor
id, channel)` in the project's stateless-roll discipline.

**Three conservation-sensitive flows**, each sweep-verified before its stage is
declared done (the committed 32-run sweep is the acceptance instrument, the
standing convention since Slice CU-1 — verify at sweep scale, not seed-42
alone):

1. **Settle payment** (§3) — segment `Wealth` → habitat construction wages.
2. **Wage redirect** (§3) — satellite-facility wages → resident segments
   instead of port households.
3. **Graduation cost** (§4) — `ExpansionPoints` spent, discounted, on
   promotion.

Each moves *where* value lands, never *how much* exists. `ConservationTests`
stays green throughout.

**Determinism**: Stage 1's hex scan is a fixed deterministic spiral, previews
pure; Stage 2/3 rolls are stateless and keyed; `SimState.Outposts` iterates in
id order (P6). Same-config runs stay byte-identical (the standing gate).

## §6 REPL surface and eyeball gate

A new `domain <port>` view shows the region as a living thing:

- **Satellite hexes** with their facilities and output.
- **Outposts** with their resident segments and founding year.
- **Candidacy status** per outpost — interior (never graduates) vs. frontier
  (eligible, distance-to-nearest-port vs. `G`).
- Settle and graduation **events** in the history/news output.

The eyeball (the taste gate): a domain **visibly blooming** over epochs; an
outpost **forming** where work concentrated; a frontier outpost **graduating**
into a starport while interior ones stay subordinate; and — the anti-goal made
visible — **no two ports adjacent.**

## §7 Testing

TDD per task; goldens re-frozen **once** at slice end (siting output
legitimately changes); the ensemble bar (`SIMHEALTH.md`) governs any tuning
claim, per standing convention.

- **Stage 1**: siting determinism byte-identity for same config; coverage that
  a richer *neighbor* hex outcompetes a depleted or fully-claimed **port hex**
  for an extraction type (the overflow case, generalized); support/processing
  types still cluster at the port; the corp-scan owner-filter fix (a corp's
  home-domain scan returns real candidates, not the empty list the naive reuse
  yields).
- **Stage 2**: the staffing-weight rewire (a resident crews its hex at full
  weight, a port household weakly); wage-redirect conservation at sweep scale;
  settle-election world-time behavior (sustained unmet labor over world-years
  triggers; a brief spike does not).
- **Stage 3**: the frontier gate (an interior outpost **never** becomes a
  candidate, at any config); the cost discount (a facility-rich outpost costs
  less than a bare one); promotion integrity (port + market born, segments
  re-attached, facilities re-resolve to the new market, encroachment tension
  fires).

## §8 Boundary — NOT this slice

- **Atlas rendering** of blooming domains, outposts, satellite workings, and
  graduation — deferred to the future atlas pass (the REPL is this slice's
  eyeball).
- **Ongoing intra-domain population churn** beyond the one settle election —
  segments do not continuously re-sort across a domain's hexes; only the
  settle event moves population finer. (The natural follow-on, §Forward
  roadmap.)
- **Corp expansion beyond the home domain** — the gate-pair / cross-border lane
  machinery (`CorporationOps.InvestGateLanes`) is untouched; corps scan their
  home domain, not others'.
- **Passenger-ship migration** — population still relocates as an accounting
  event, not as cargo on a hull (carried from the locality slice's own
  boundary).
- **Outposts as war objectives** beyond what falls out of their facilities
  being ordinary contestable `Facility` rows — no new siege/objective type.
- **Off-lane / patrol behavior** beyond consuming the locality slice's existing
  `StaffingOps`/`OrbitGeometry` machinery.

## Forward roadmap

- **Atlas surface** — the deferred visual pass: domains rendered as filling-in
  glows, satellite workings and outposts at their real hexes, a graduation
  animation. This slice deliberately produces the *state* an atlas pass will
  read (outpost records, segment hexes) so that pass is pure rendering.
- **Intra-domain population churn** — the natural follow-on to Stage 2's single
  settle election: segments continuously re-sorting across a domain's hexes as
  work shifts, the finer-grained cousin of domain-to-domain migration. Flagged,
  not decided.

## Open implementation choices (decided at plan time, not reopened here)

- Whether the settle election (§3) reuses/extends the existing migration
  machinery or gets its own pass.
- The exact parameterization of `G` (§4) — which service radii and which
  margin — exposed as **registered knobs** (`KnobRegistry`; an unregistered
  knob silently reverts on reload, so `G`'s inputs must be in the table).
- The exact form of the **hauling-cost proxy** in the Stage 1 score (§2).

## Provided interface

- `CapabilityOps.ConstructionCandidatesFor` (and the corp path in `Phases.cs`)
  scan **per hex** across a domain, body-aware, with a preview
  (`Generator.Generate` without commit) for unsettled hexes and real
  `SettledSystems`/`BodyResources` for settled ones. Owner-filter scoped by
  home-port domain so corp scans return real candidates.
- `PopulationSegment.Hex` — a segment's settled hex within its administering
  domain (defaults to the port hex; serialized, segments layer v4).
- `SimState.Outposts` — the lightweight outpost registry `(id, name, hex,
  parent port id, founding year, graduated flag)`; not an actor; new
  serializer layer.
- `PopulationSiting` extended to resolve a body within an arbitrary domain
  hex's committed system, not only the port's.
- `StaffingOps.ProximityWeight` measures `segment-hex → facility-hex`.
- Satellite-facility wages redirect to resident segments
  (`MarketEngine.PayWages`).
- An administrative **graduation project** kind: no convoy, `ExpansionPoints`
  cost discounted by existing facilities/population, completing into a tier-1
  `Port` + `Market` with segment re-attachment and `CompleteExpedition`'s
  encroachment-tension bump.
- A `domain <port>` REPL view and a `SIMHEALTH.md`-tracked outpost/graduation
  metric.
