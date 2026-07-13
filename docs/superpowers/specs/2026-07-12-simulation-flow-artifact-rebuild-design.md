# Simulation-Flow Artifact Rebuild — Design Spec (2026-07-12)

Design record for the ground-up rebuild of the living baseline diagram
(`docs/diagrams/generation-flow.html`). This spec is the content + structure
brief a later implementation session translates directly into HTML. It does
**not** author HTML, CSS, or pixel values — those are the implementer's, guided
by the continuity notes in the last section.

## 1. Goals & scope

The current artifact was built early (its stamp reads "slices A–J merged; only
K remains") and is now stale in two ways: the atlas is marked PoC when K1–K5
have since delivered the full atlas and merged; and it presents the simulation
almost entirely as a **temporal pipeline**, with the subsystems collapsed into
a single "epoch simulation" band whose internal mechanics are invisible. The
design tree under `docs/design/` has since deepened to ~24 docs naming specific
models (order-book markets, the chassis grid, the temperament composition, the
POI compiler…), and none of that specificity survives in the picture.

The rebuild represents **the simulation in its entirety**, two ways at once:

- as the **temporal step sequence** (the flow chart the user already has), and
- as a **block diagram of every mechanic and its interfaces** — naming the
  actual model each mechanic uses, not a generic subsystem label.

Completeness is the bar: every mechanic worth a node gets one (target ~75–80
nodes), each with a terse one-line model tag and a status. Gaps in this
inventory become gaps in the eventual diagram, so the inventory in §4 is the
load-bearing part of this document.

**Non-goals.** No CSS/layout math, no exact colors or spacing (continuity notes
only). No re-litigating the design itself — where a design doc flags a
deviation, that flag becomes a node status, verbatim in intent; we do not
invent new gaps or "fix" the design here.

## 2. The two-view architecture (locked)

Same self-contained HTML/CSS artifact style as today: no SVG, no JS beyond a
view toggle, CSS grid/flexbox only. Same file path
`docs/diagrams/generation-flow.html`, republished to the **same living artifact
URL** (tracked in project memory: `generation-flow-artifact.md`) with that URL
passed explicitly at publish so the link is stable. Keep the 🌌 favicon and the
`<title>` unchanged. Bump the header stamp (date + commit) and the status chips.

A **toggle** (tab/switch in the header) flips between two views that share one
CSS system so they read as one artifact:

### View 1 — Flow (the temporal spine)

The pipeline the current artifact has, re-cut so each phase card is expandable
into its mechanics.

- **Header strip: the four clocks** (`frame/time.md`): cosmic → evolutionary →
  generational → play, each a card with span/step and "hands to the next."
  This is the existing clock-stack chain, preserved.
- **The upstream pipeline chain** (existing spine, kept):
  `GalaxyConfig → density field → galaxy skeleton → seeding passes →
  per-hex generation (pure fn, never persisted)`. Per-hex generation is the
  Phase-1 pipeline and is explicitly labeled *never persisted*.
- **The epoch-sim master loop** — the **seven phases** of
  `frame/simulation-flow.md` as the main chain:
  Perception → Markets → Allocation → Intent → Resolution → Interior &
  Demographics → Chronicle. Each phase card **expands** to list the specific
  mechanics that fire in it that step (see §4's Phase column), each with its
  one-line model tag. The loop's back-edge (Chronicle → next step's Perception)
  is a labeled text connector.
- **Inspection & Rendering** sits downstream as the consuming surface: the
  Inspector REPL, the Unity atlas (now built, not PoC), and the sim-health
  harness.

### View 2 — Blocks (the architectural spine)

Built off `frame/system-map.md`'s own five-level table.

- **Genesis** as the upstream layer (cosmic + evolutionary/precursor), feeding
  the levels below.
- **Level cards**: L0 Substrate · L1 Economy · L2 Polity interior · L3
  Inter-polity · L4 Narrative. Each card carries its **Owns / Provides / Reads**
  row straight from `system-map.md`, and **expands** into its constituent
  mechanic nodes (§4), each a one-liner model tag in the same style as Flow.
- **Cross-cutting components**: the **Fleet model** (owned by neither L1 nor L3,
  `system-map.md` §fleet + `fleets/ships-and-fleets.md`) and the **Actor
  taxonomy** (`frame/actors.md`) render as cross-cutting cards spanning the
  levels.
- **The cross-cutting interfaces strip**: the four frame interfaces —
  Controller contract, price signal, event grammar, pressure → graduation
  (`system-map.md` §cross-cutting) — as a dedicated strip.
- **Interfaces between levels render as labeled TEXT connectors** ("reads →",
  "provides →") between cards, not drawn SVG edges — no coordinate math,
  consistent with the no-SVG constraint. Every connector's label comes from the
  providing side's Provides column (interfaces documented on the providing side,
  per `design/README.md`).

Both views draw their nodes from **one shared inventory** (§4). A mechanic's
model tag and status are identical in both views; the only per-view difference
is grouping (by phase vs. by level) and the connector labels.

## 3. Status taxonomy & its sourcing

Every node in both views carries exactly one status. Three states (mapping onto
the existing artifact's chip styling — see §5):

- **Implemented** — built, armed, and matching the design. Certified by the
  slice-J acceptance pass (P1–P8 all certified) and the A–K roadmap close
  (HANDOFF 2026-07-12). The large majority of nodes.
- **Specced-only** — the design specifies it but **no implementation exists**
  (a structural slot may exist but is inert). The gap is total absence.
- **Known gap** — **implemented but flagged**: a deviation from, or shortfall
  against, the design that a design doc or HANDOFF explicitly records. A
  stand-in is running; it does not yet match the spec.

The two non-Implemented states are distinguished by a single test: *is any
implementation present?* None → Specced-only; present-but-flawed → Known gap.

**Sourcing — every non-Implemented node is traceable to one of three sources.**
No node invents a status; each cites where the flag lives.

1. **The filed gap list** — `docs/superpowers/specs/2026-07-11-design-acceptance.md`
   §"The gap list" (13 numbered gaps). Cited as *gap N*.
2. **HANDOFF carried/flagged items** — `docs/HANDOFF.md` §"Carried / flagged"
   and the SH treasury-spiral diagnosis. Cited as *HANDOFF §carried* or
   *HANDOFF/SH*.
3. **Self-flagged deviations written into the design docs themselves** — the
   "Deviation, flagged: …" / "Scoped, flagged: …" / "Kept … flagged" notes that
   already live in specific docs. Cited by doc + section. These become Known-gap
   nodes directly; we do not restate or expand them.

The mapping of each gap to its status:

| Gap (source) | Node | Status | Rationale |
|---|---|---|---|
| gap 1 | 11 unarmed contract acts | Specced-only | records exist, no Resolution path |
| gap 2 | perceived-price arbitrage | Specced-only | belief carries no partner prices; freight clears on true prices |
| gap 3 | procurement contract objects | Known gap | escrowed contracts stand in as mechanical stockpile-target procurement |
| gap 4 | sanctions closure | Specced-only | lane-legality closure machinery absent (the act is gap 1) |
| gap 5 | sentient trafficking | Specced-only | unmodeled in both commodities and migration |
| gap 6 | perishability | Known gap | design says decay compounds; reserves store loss-free |
| gap 7 | culture drift | Known gap | mint-at-schism works; separation-split & slow blending undone |
| gap 8 | plague depth | Known gap | base loop works; excavation-release, Medicine mitigation, memorial POIs, era-signature missing |
| gap 9 | war depth | Known gap | theater/objective works; occupation objectives, defensive mirrors, raidable supply objectives, commander boldness, personal unions missing |
| gap 10 | fleet depth | Known gap | Carrier role & refit variants unused; piracy risk not priced into freight profit |
| gap 11 | automation | Known gap | production formula accepts it; Markets passes 0.0 |
| gap 12 | courier fast-paths | Specced-only | news travels traffic only; couriers/scouts/news-carrying player are play-clock |
| gap 13 | espionage | Specced-only | reserved by design (technology.md, characters.md) — recorded for completeness |
| markets.md §market-step 3 | per-owner quote decay | Known gap | "Deviation, flagged" — discovery lives in reference drift instead |
| markets.md §courier | courier contract ranking | Known gap | "Deviation, flagged" — ranks (priority, id), fee prices poster cost only |
| markets.md §demand-bids relay bids | relay bids | Known gap | "Kept past B2, flagged" — hop diffusion stand-in, retires with multi-hop actor runs |
| corporations.md §controller | corporate plan scope | Known gap | "Scoped, flagged" — plans cover facilities; routes/gates opportunistic, hulls immediate |
| HANDOFF/SH | credit market / treasury spiral | Known gap | structural debt spiral; LoanRatePerYear a dead knob; 2×-lender gate kills the credit market epochs 1–4 |
| HANDOFF §carried (CE C17) | capital-goods chains | Known gap | anemic; stalled InTransit couriers can lock fee+cargo |

Note: `infrastructure.md`'s port-upgrade exotics note reads "Amended in slice CE"
— that is a **landed amendment**, not a flag; the port-upgrade node is
Implemented. Atlas-surface flags in HANDOFF (K4 timeline switch-back, menu
F1–F4 stubs, per-lens readability) attach to the Inspection & Rendering nodes,
not to sim mechanics.

## 4. The content inventory

Organized by **Blocks-view level** (the architectural spine). Each row gives the
node name, its **model/mechanism tag** (the one-liner the diagram prints), the
**Flow-view phase(s)** it fires in, its **status**, and its **key interfaces**
(reads ← / provides →). The Flow view groups these same nodes by the Phase
column; §4.9 gives the per-phase roster for the Flow chain.

Phase legend: Pc=Perception, Mk=Markets, Al=Allocation, In=Intent,
Rs=Resolution, Id=Interior & Demographics, Ch=Chronicle, Up=upstream
(pre-loop), X=cross-cutting/no single phase.

### 4.1 Genesis (upstream layer) — `genesis/`

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Cosmic field stack | conserved per-cell fields (Gas, star cohorts, Metals, Remnants); step loop inflow→transport→star-formation→aging→death&enrichment over ~150 deep-time steps | Up | Implemented | → present-day cell fields (density, metallicity, mineral richness, stellar lean) to substrate & hex tier |
| Cosmic discrete features | seeded/emergent registry: mergers (stellar streams), globulars, nebulae, AGN accretion epochs | Up | Implemented | → feature registry + hex-tier star-table overrides |
| Habitability history | per-cell scalars: metallicity-floor crossing, last sterilization, stability-since — makes emergence causal | Up | Implemented | → evolutionary clock |
| Biosphere field | per-cell LifeViability/BiosphereAge/Richness/SapiencePotential; step loop abiogenesis→aging→catastrophes→spread→sapience-registration | Up | Implemented | reads ← cosmic fields; → biosphere layers |
| Emergence schedule | each sapient origin's spaceflight date = abiogenesis + richness-scaled maturation; staggered polity entry with late-emerger contact bonus | Up → Id | Implemented | → epoch sim (new polities join in Interior) |
| Precursor waves | coarse civ-arc sim (rise/peak/decline) reusing the space model without markets/characters; vigor classes (grand/pocket); cause-typed endings | Up | Implemented | → precursor registry, deep-time chronicle |
| Precursor living residue | machine descendants (seed a present machine-species origin), biosphere engineering, sterilization scars, dormant remnants | Up | Implemented | → hex-tier anchors (pre-commitments), POI sources |

### 4.2 L0 Substrate — `substrate/`, `frame/space-and-travel.md`

Owns: commodity vocabulary, infrastructure vocabulary & siting, natural raster,
population stores, lane geometry, market geography. Reads: deep-genesis outputs.
Provides: `Potential(cell, good)`, habitability, demand profiles, buildable
catalog.

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Commodity vocabulary | 17 goods in raw/processed/capital tiers; recipe chains 1–4 nodes deep; standard (exotics-free) vs advanced (exotics-gated) variants, tech-tier-gated | Mk | Implemented | → markets, war, SoL, tech |
| Grade system | every stock = (quantity, grade∈[0,1]); grade flows terrain→chains; `Effective(useCase)=qty×GradeMultiplier`; tech tier is the grade ceiling; precursor grade above any ceiling | X | Implemented | → markets (value density), war, SoL, tech, retail |
| Demand model | priority bands: population (subsistence/SoL/luxury, embodiment-modulated) · industry · movement (Fuel) · military · technology (Refined Exotics × Compute) | Mk | Implemented | reads ← population, facilities; → markets |
| Legality schema | per-polity law code legal/restricted/prohibited + tariff; prohibition → black-market demand at margin | Mk | Implemented | → markets, smuggling, relations |
| Sentient trafficking | illicit population flow against the gradient toward low-rights polities; crime vs the population substrate | Mk/Id | Specced-only (gap 5) | (unmodeled) |
| Infrastructure catalog | 15 types in 5 families (keystone port + extraction/processing/heavy/support); each tier 1–3, build cost, construction time, upkeep, hex anchor; siting rules | X | Implemented | → economy (lifecycle), war (objectives) |
| Port & domain model | keystone port: local service radius + gate slots (two growth axes); territory = union of port service areas (derived, never stored); domain overlap = contested zone | X | Implemented | → territory, lanes, markets, relations |
| Lane / gate model | a lane = linked gate pair (one per port system); reach = min gate tier; capacity/speed from tiers; anti-web rule; crossing fees by gate owner | Rs/Mk | Implemented | → freight, tariffs, interdiction |
| Production formula | `output = base(type,tier) × terrain × labor × machineryGrade × automation(compute)` | Mk | Implemented (automation term Known gap 11) | reads ← raster, population, tech |
| Organic baseline | unserviced settlements subsistence-farm/craft locally; small enough that facilities dominate | Mk | Implemented | → household income |
| Market geography | one market per port at service-area∩lane-network; per-good price/last-cleared-qty/mean-grade + black book; connectivity is price structure; wilds have no market | Mk | Implemented | → markets, price map |
| Retail projection | play-clock items are retail instances sampling local (good,grade,qty) stocks; tail-sampled exceptional items | X (play) | Specced-only (play-clock) | reads ← market stocks |

### 4.3 L1 Economy — `economy/`

Owns: markets, trade flows, wealth ledgers, corporate registry, infrastructure
registries, tariffs/sanctions. Provides: prices, tax income, corporate revenue,
port domains & lane network, throughput, freight capacity, interdiction strain.
Reads: L0 potentials/demand; L3 constraints; the fleet model; Intent policies.

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Order-book market engine | the market IS the set of open buy/sell orders (EVE model); physical escrow (sells hold goods, buys hold credits); reference price is the persistent readout | Mk | Implemented | → order books, reference prices |
| The market step | fixed 9-step order: expiry sweep → freight sail → requote → supply lands → escrowed demand bids → spread run → matching (price-time priority, MAKER-price fill) → reference drift → clearing consequences | Mk | Implemented | → fills, famine/SoL/underproduction signals |
| Per-owner quote decay | (design) sold-out sellers raise, glutted cut | Mk | Known gap (markets.md §step 3 deviation) | discovery lives in reference drift |
| Relay bids | cheap-end sovereign bids at own reference to stage re-export; hop-by-hop diffusion; entrepôts emerge | Mk | Known gap (markets.md, "Kept past B2, flagged") | retires with multi-hop actor runs |
| Freight / shipments | a haul = Shipment (origin, dest, cargo, lane route); leg-years priced at departure; blockade/quarantine/dead-gate stalls it; piracy/war-interdiction rolls per sail | Mk | Implemented | reads ← lanes, fleets, war; → connected-market convergence |
| Spread run | posted fleet's owner trades its lane gradient with own capital: lift cheap asks, sail, post at dear end; absorption reads real resting bids above delivered break-even | Mk | Implemented | reads ← order books, fleets |
| Courier contracts | internal logistics as a market: courier posts (origin, dest, escrowed cargo+fee); board clears (priority, id); War priority outbids commerce; requisition channel rides it | Mk/Al | Implemented (ranking Known gap) | → job board; ranking deviation (markets.md §courier) |
| Household income & labor share | facilities pay a labor share of revenue to staffing segments (shrinks with automation); SoL derives from real wages at local prices | Mk | Implemented | → population SoL, migration opportunity term |
| Wealth & taxation | transaction tax on sales + tariffs on cross-border freight + state-facility income; true wealth = ledger + asset book (emergent readout) | Mk | Implemented | → polity income |
| Credit / loans | loan objects (lender, borrower, principal, rate, term); default → reputation/relations hit, collateral seizure; no banks, lenders are whoever holds surplus | Al | Known gap (HANDOFF/SH treasury spiral) | structural debt spiral; LoanRatePerYear a dead knob; 2×-lender gate kills the market |
| Stockpiles & procurement | stock has an address (resting order / larder / laydown yard / in-transit); per-port procurement toward standing targets from reserve treasury; depot tiers bank & slow decay; local siege buffering | Mk/Al | Implemented (perishability Known gap 6; procurement-contract objects Known gap 3 — mechanical stockpile-target stand-in) | reads ← Intent targets; → siege endurance, famine buffer |
| Interdiction strain | per-lane realized-vs-potential trade value minus smuggling leakage; measured where it happens | Mk | Implemented | → war weariness, relations |
| Sanctions & tariffs | tariff schedule collected at the entered gate; sanction = non-war lane-legality closure; both evadable at margin, both feed trade→relations hook | Mk/In | Tariffs Implemented; sanctions closure Specced-only (gap 4) | → relations |
| Construction / projects | every in-flight work = a project with a rate contract: BuildCost ÷ ConstructionYears per-year basket + wages; bids as a market participant into a laydown yard; advances by scarcest-input fraction; priority-ordered feeds; abandon clock | Al/Mk | Implemented | reads ← standing plan; → completions (facilities, ports, lanes, hulls, colonies) |
| Plan packing | standing plan packed against real capability: income/yr + savings drawdown; colony batches boosted to front when expansion points sit hull-less | In → Al | Implemented | reads ← perceived capability brief |
| Condition & ownership | facilities carry condition (decays w/o upkeep, war-damaged, repaired); ownership transfers by sale/seizure/nationalization/conquest, each a conserved ledger event | Al/Rs | Implemented | → ruins (residue), atlas overlays |
| Technology | 4 per-polity domains (Industrial/Military/Astrogation/Life); geometric tier ladders unlock ceilings/regions; research consumes Refined Exotics × Compute in Allocation; diffusion via trade/salvage/espionage | Al | Implemented (espionage channel Specced-only gap 13) | → `Ceiling`/`Region` to grade, recipes, ships, ports |

### 4.4 L1 Economy — Corporations & outlaws — `economy/corporations.md`

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Corporate founding | simulation watches persistent profit niches (price gradient / unexploited deposit / unserved route) over consecutive epochs; charter event via graduation; niche stamps character | Id | Implemented | reads ← markets, charter policy; via graduation |
| Corporate controller | standing plan (polity planner machinery at corp scope) packed against income + savings drawdown; dividend rate; lobby targets; risk appetite | In | Implemented (plan scope Known gap) | scope deviation (corporations.md §controller) |
| Portfolio & operations | owns facilities/freighters/depots/routes across borders; speculation is the business (spread runs on own capital); internal logistics on courier contracts; vertical integration | Mk/Al | Implemented | reads ← order books, fleets |
| Corporate influence | lobby spending strengthens aligned factions (dividends → elite faction wealth); sanction evasion by re-flagging through subsidiaries | Id | Implemented | → factions, relations |
| Corporate death & estates | bankruptcy (default cascade) / nationalization / niche death; estates-pass settles orders, cargo, jobs, projects, credits, debt conservatively | Id/Rs | Implemented | → successor sovereign, residue |
| Outlaw institutions | same niche rule founds cartels (black-book) & pirate bands (raiding niche = lawlessness × cargo value); based at ruin/haven POIs | Id/Mk | Implemented (piracy-risk-pricing Known gap 10) | → smuggling, lane piracy |

### 4.5 Fleet model (cross-cutting component) — `fleets/ships-and-fleets.md`

Consumed by L1 (freight, piracy risk) and L3 (combat vectors); owned by neither.

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Chassis grid | design = role × size cell (Freight/Escort/Line/Carrier/Scout/Colony/Special); instantiated per polity from embodiment × culture × tech × grade | X | Implemented (Carrier role Known gap 10) | → design registry |
| Design sheet | two-layer stat model: ~15-stat sheet (Combat/Mobility/Capacity/Operations) + epoch aggregation into vectors; grade/tech act per-stat; refit variants | X | Implemented (refit variants Known gap 10) | → war vectors, play-clock ship |
| Design lineages | designs drift along named lineages/marks over epochs — fleet composition reads as cultural history | X | Implemented | → chronicle color |
| Hull production | shipyards convert Ship Components (+Armaments/+Compute) into hulls; hull-batch project anchored at a yard; yard tier caps concurrent batch work | Al | Implemented | reads ← standing plan, markets |
| Fleet object & postures | `(id, owner, location, composition, posture, commander, supply)`; postures Posted/Escort/Patrol/Blockade/Expedition-Convoy/Reserve | Mk/Rs | Implemented | → Markets (freight/risk), war |
| Movement & supply | three leg types (intra-domain/lane-hop/off-lane); off-lane gated on endurance floor; fleets draw fuel/upkeep from nearest owned port; unsupplied lose readiness then hulls | Rs | Implemented | reads ← space model, markets |
| Attrition & wreckage | losses conserve into wreckage at the death hex → salvage sites & battlefield POIs; piracy risk/lane = lawlessness × cargo value − escort vectors | Rs/Ch | Implemented (piracy-risk-into-profit Known gap 10) | → POI compiler, tech salvage |
| Information carriage | news speed/lane = f(posted traffic frequency); courier/scout fleets are deliberate info assets; player carrying news is this at individual scale | Pc/Ch | Implemented (courier fast-paths Specced-only gap 12) | → Perception freshness |
| Commanders | fleets above a threshold take a commander role; personality biases posture AI; renown accrues; age/die/succeed/defect | In/Id | Implemented (boldness bias Known gap 9) | via role bridge to characters |

### 4.6 L2 Polity interior — `polity/`

Owns: factions, ideology, government form, characters/roles, succession,
cohesion, demographics. Provides: temperament composition, stability/schism
risk, graduations, leadership personality. Reads: L1 (SoL, faction wealth), L3
(war outcomes), L4 (news → opinion).

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Population segments | `(species, culture, size, SoL, ideology distribution)`; domain-level state (hex is a projection); conserved, identity travels; mixed by conquest/migration/diaspora | Id/Mk | Implemented | → demand side, faction base, migration |
| Demographics | growth = f(SoL, provisions, embodiment); machine populations grow by manufacture (Machinery+Compute), age out when cut off; famine/war shrink | Id | Implemented | reads ← markets, plagues |
| Culture | registry entities, species-rooted, syllable flavor names systems/ships/characters; spread by migration/conquest | Id | Implemented; drift Known gap (gap 7) | slow blending & separation-split undone |
| Ideology | 4 axes (Authority↔Autonomy · Communal↔Individual · Open↔Insular · Sacral↔Material); segment distributions drift with lived conditions; official = weighted opinion × institutional inertia | Id | Implemented | → temperament, factions |
| Migration | per-step segment flows along SoL/safety/affinity/opportunity gradients × distance/lane access; refugees (fast) & diasporas (memory-carrying minorities) | Id | Implemented (trafficking Specced-only gap 5) | reads ← wages, war, famine |
| Faction formation | coalesces when a coherent interest diverges from rule; six bases (ideological/cultural/regional/corporate/military/sacral); state = basis, strength, agenda, militancy | Id | Implemented | reads ← population, dividends, veterans |
| Faction pressure | policies drift toward strong factions (bounded); appeasement spending buys off; unappeased accumulate grievance | Id | Implemented | → Intent AI weighted pull |
| Graduation | `strength × grievance > legitimacy × enforcement` → Schism (polity) / Coup (ruler, → civil war) / Charter (corp) / Revolt (failed) | Id | Implemented | → new institutions (the one factory) |
| Legitimacy & cohesion | legitimacy = f(SoL trend, ideology gap, war outcomes, prestige, cultural accommodation); cohesion = aggregate × structural strain; low cohesion lowers graduation thresholds | Id | Implemented | → graduation, stances |
| Government forms | closed catalog of 8 (Autocracy/Collective/Assembly/Syndicate/Theocracy/Hive Unity/Machine Consensus/Steward Dynasty) seated in ideology × species; sets succession, inertia, faction tolerance; changes through graduation | Id | Implemented | → temperament weights, succession |
| Temperament composition | Intent-AI personality = species disposition × official ideology × ruler personality × faction pressure, weighted by government form | In | Implemented | → every Intent decision |
| Characters | sparse; generated on demand from (institution, culture, species, seed); personality = ideology position + boldness/zeal/competence/ambition; lifespan/succession; dynasties; notables (hero/founder/prophet/pirate-lord/magnate/explorer); derivable biography | Id/In | Implemented (personal acts Specced-only gap 1) | via role bridge; → temperament ruler term |
| Plagues | outbreak ∝ density × SoL deficit × exposure; propagates the lane graph with traffic; Medicine mitigation; quarantine = self-imposed interdiction; burns out; memorial residue | Id | Implemented; depth Known gap (gap 8) | excavation-release, Medicine mitigation, memorial POIs, era-signature missing |

### 4.7 L3 Inter-polity — `interpolity/`

Owns: relations matrix, wars, treaties/federations/vassalage, military postures,
fronts, battles. Provides: constraint surfaces for L1 (blockades/borders/
sanctions), war outcomes for L2, contact events. Reads: L4 stances, L1 prices,
L2 composition, the fleet model.

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Contact | polities meet when reach overlaps (expansion/trade/news); first contact composes stance from temperament × strangeness × pre-arrived reputation | Id/Rs | Implemented | reads ← perception, temperament |
| Native policy | on covering a pre-spaceflight homeworld: protectorate / integrate / exploit / uplift (Intent act, ideology-weighted, reputation-bearing) | In/Rs | Implemented | → emergence resolution |
| Late-emergence resolution | emergence in free space = new polity; inside claimed space resolves by host native policy (vassal/autonomous member/suppressed + liberation casus belli) | Id | Implemented | reads ← emergence schedule, native policy |
| Expansion prices neighbors | colony valuation discounts a site per entangled foreign domain; founding costs instant tension with each; borders contiguous by choice | In/Rs | Implemented | reads ← price signal, terrain; → tension |
| Relations state | per-pair warmth (interdependence) + tension (friction, the war-pressure gauge, decays only when sources resolve); treaty rungs (trade pact / non-aggression / defense alliance / federation-vassalage) at mutual consent | Id/Rs | Implemented | reads ← trade volume, claims, ideology; → war pressure |
| Federation | merge gate (sustained alliance + warmth + ideology compat + openness + cohesion); entangled friendly borders push to fusion; fused polity is NEW (weighted composition, fresh form) | Rs | Implemented | reads ← relations, cohesion |
| Vassalage | asymmetric rung: imposed by settlement or chosen under threat; tribute/defense/policy-lock; exits absorption (drift) & secession (bid) | Rs | Implemented | reads ← war, relations |
| Dynastic instruments | marriages/wardships buy warmth & create succession claims (tension pointed the other way); rare personal unions = federation fast-path | Rs | Implemented (personal unions Known gap 9) | reads ← dynasties |
| War causes | tension discharges through a casus belli menu (economic/ideological/political/spatial/spark); spark rolls in high-friction space; aims scale with hatred → annihilation when saturated | In/Rs | Implemented | reads ← prices, identity, factions, space model |
| War conduct | theater/objective model: assignment per doctrine+commander → per-objective engagement on fleet vectors (fortification, supply, competence, rolls) → sieges (reserves, fortress tier, relief); mobilization is a ramp not a switch | Rs | Implemented; depth Known gap (gap 9) | occupation objectives, defensive mirrors, raidable supply objectives missing |
| Front supply lines | war force draws upkeep from nearest owned port (forward depot); quartermaster stocks it via War-priority couriers; war interdiction rolls seizure per contested sail, escorts damp deterministically; starvation bites readiness | Rs/Mk | Implemented | reads ← courier board, fleets; → readiness |
| Allied belligerents | defense-alliance partners join as supporters under war leaders; settlements negotiated between leaders; allied gains/grievances flow through the leader's table | Rs | Implemented | → alliance warmth/tension |
| Termination & settlement | break on political collapse / exhaustion / capital loss / extinction; settlement negotiated from per-objective outcomes (cede/reparations/vassalize/imposed legality/white peace); accept when perceived cost > settlement (stale, so wars overrun) | Rs | Implemented (imposed-legality Known gap 9) | reads ← perception |
| War aftermath | grudges → standing claims (tomorrow's tension); veterans → military factions; heroes mint; wreckage/razed → POIs; conduct reputation travels the news graph | Ch/Id | Implemented | → relations, factions, POIs, reputation |

### 4.8 L4 Narrative — `narrative/`

Owns: event log, news pulses, per-actor perception, reputation, chronicle/era
views, POI compiler, world-state handoff. Provides: perceived state, chronicle
queries, POIs to the hex tier, the handoff. Reads: everything, via events only.

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| News pulses | Chronicle emits pulses for public events above magnitude floors; travel the lane graph at traffic-derived speed, attenuating with distance; couriers/scouts/player are fast paths | Ch/Pc | Implemented (fast paths Specced-only gap 12) | → perception |
| Perception state | per-actor compressed beliefs: stance table + belief snapshots per (observer, subject) frozen at last refresh (refresh when elapsed years cover news delay); self-facts fresh; corporate perceives own capability | Pc | Implemented (perceived-price arbitrage Specced-only gap 2) | → Intent reads truth through snapshots |
| Stances & reputation | news arrival updates stance filtered through observer temperament; reputation derived (never stored) as per-audience stance aggregates, consumed as gates | Pc | Implemented | → treaty ascent, charter, hiring |
| Event grammar | one schema across four clocks: `(id, world-year, clock stratum, type, actors[], location, magnitude, valence, visibility, payload)`; 8 type families; visibility public/regional/secret; indexes are views | Ch | Implemented | the P4 backbone; → all indexes |
| Chronicle views | queries over the one log at every zoom: galaxy (era detection clusters epochs by signature) / polity (reign arc) / character (biography) / place (hex annotation & archaeology) | Ch | Implemented | reads ← event log |
| POI compiler | runs inside Chronicle every epoch, converting qualifying events into anchored POIs immediately (battlefields→salvage, ruins→lawlessness, razed capitals→claim anchors, memorials, precursor sites); live sim effects; one-anchor-per-hex by magnitude; decay as consumed | Ch | Implemented | → hex tier, economy (salvage niches), relations |
| World-state handoff | final artifact layer: complete registries + deliberately open threads; resumability (same machine at play tick); controller handover; log never closes; delta boundary (save = config + artifact + deltas + log continuation) | X | Implemented | → the play clock / game layer |

### 4.9 Cross-cutting interfaces strip — `frame/system-map.md` §cross-cutting

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Controller contract | `Decide(perceivedState) → (policies, acts)` per actor kind (polity/corp/character); the Intent-phase API = the player UI surface; enumerated in `frame/controller-contract.md` | In | Implemented (11 unarmed acts Specced-only gap 1) | armed acts: found-colony, declare-war, treaty, settlement-response, nationalize, vassalage, dynastic instrument, quarantine |
| Price signal | market-price-derived valuations in grade-effective units are the one value language: expansion attractiveness, war-goal value, migration pull, investment, siting all read it | X | Implemented | ← markets; → every valuation |
| Event grammar (interface) | every subsystem emits one schema; L4 owns it; emitting well-formed history is a requirement on every mechanic | Ch | Implemented | (see 4.8) |
| Pressure → graduation | L2 faction machinery is the sole factory for new institutions; L3 consumes schisms, L1 consumes charters; emergence schedule is the one non-faction origin | Id | Implemented | → polities, corporations |

### 4.10 Determinism & artifact discipline (frame, cross-cutting) — P6

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Determinism discipline | stateless hash rolls keyed (step, actor id, channel 0–73); fixed iteration order; config artifact-stamped; byte-identity at coarse & fine ticks | X | Implemented | governs every phase |
| Artifact layers | ~22 versioned registry layers (ports, lanes, facilities, designs, fleets, wreckage, segments, markets, loans, characters, dynasties, factions, corporations, relations, wars, beliefs, pulses, POIs, plagues…); delta saves = base + changed layers + log continuation; hex tier never persisted | X | Implemented | the persisted state |
| Four-clock rate model | all rates in world-years (P7); epoch = 25y integration step, not a unit; durations are world-time state; coarse/fine sample the same durations | X | Implemented | governs time.md |

### 4.11 Inspection & Rendering (downstream consuming surface)

| Node | Model tag | Phase | Status | Interfaces |
|---|---|---|---|---|
| Inspector REPL | epoch run/save/load (22-layer), watch, emap layers, panels, threads, estep (fine tick), delta saves, belief/news/era/poi queries | X | Implemented | reads ← artifact |
| Unity atlas | rebuilt against the settled model (K1–K5 merged): skeleton → lenses → panels → timeline → system stage; five LOD bands; map/system-stage lenses; typed pickables → panels | X | Implemented (was PoC — key change) | reads ← Core Atlas queries |
| Sim-health harness | MetricRegistry + per-phase MoneyRow; conservation residual ≈1e-8; ensemble sweep runner; `ehealth`; in-memory only, never serialized | X | Implemented | reads ← SimState.Health |
| Game layer / play clock | live game: player verbs, perceived-price trading, news-carrying; the acceptance gap list | X | Specced-only (future) | → controller slots |

## 5. What changes from the current artifact

**Preserved (continuity — do not reinvent):**

- The self-contained HTML/CSS style: no SVG, no external assets, CSS grid/
  flexbox, the starfield `body::before`, the light/dark theme variables, the
  `--done/--proto/--spec/--fut` chip palette and legend, the mono/sans type
  pairing, the tier-band + node-card + chain-scroll structural vocabulary.
- File path `docs/diagrams/generation-flow.html`; the 🌌 favicon and the
  `<title>`; the living artifact URL (republished in place).
- The four-clock header stack and the upstream pipeline spine
  (GalaxyConfig → density field → skeleton → seeding → per-hex generation).
- The footer "sources of truth" block (updated to point at the `docs/design/`
  tree as primary).

**Replaced:**

- The single collapsed "epoch simulation · master frame" band becomes the
  **seven expandable phase cards** in Flow view, each exposing its mechanics.
- The generic actor/registry/interface sub-groups become the **Blocks view**
  level cards with real Owns/Provides/Reads rows and per-mechanic model tags.
- The status stamp and every chip are re-derived from the A–K close + the gap
  list. The old **four-state** legend (Implemented / Prototype / Specced /
  Future) collapses to the **three-state** taxonomy of §3 (Implemented /
  Specced-only / Known gap). The "Prototype (PoC)" state retires — nothing is a
  running-prototype-awaiting-replacement anymore.
- The Unity atlas node flips from **PoC/proto** to **Implemented** (K1–K5
  merged) — the single most visible staleness fix.
- The "design passes — all specced/all implemented" pill chain retires; the
  A–K roadmap is closed, so pass-tracking is no longer news.

**New:**

- The **view toggle** and the entire **Blocks view** (levels, fleet & actor
  cross-cutting cards, the interface strip with labeled text connectors).
- **Model tags on every node** — the specificity the current artifact lacks
  (order-book matching, chassis grid, temperament composition, POI compiler,
  the market step's 9-step order, etc.).
- **Known-gap nodes** as first-class, each traceable to gap N / HANDOFF / a
  named doc deviation (§3 table) — the design's own promised-but-unbuilt
  backlog, rendered in the picture instead of hidden.
- The **sim-health harness** as a rendering/inspection surface node.
- Genesis rendered with its real internals (field stack, biosphere field,
  emergence schedule, precursor civ-arc sim + living residue) rather than two
  "DONE" clock cards.

## 6. Implementation notes for the later session

- One inventory, two groupings. Build the node list from §4 once (name, model
  tag, phase(s), status, interfaces); render it grouped by phase for Flow and
  by level for Blocks. A node's tag/status must be byte-identical between views
  — that consistency is a review check.
- Interface connectors are **text**, sourced from the providing side's Provides
  column. Never compute coordinates.
- Status chips reuse the existing CSS classes; map Implemented→`done`,
  Specced-only→`spec`, Known gap→a distinct treatment (the retired `proto`
  slot's styling is free to repurpose, or a dashed `spec` variant — implementer's
  call, but the three states must be visually unambiguous).
- Keep the stamp honest: date 2026-07-12, current `main` commit at publish, and
  a status line reading roughly "A–K roadmap closed; epoch sim implemented;
  13 filed gaps + flagged deviations carried."
- The design tree (`docs/design/`) governs; this page summarizes. Where this
  spec and a design doc disagree, the doc wins and this spec is the bug.
