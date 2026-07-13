# Simulation-Flow Artifact Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `docs/diagrams/generation-flow.html` as a single self-contained two-view HTML artifact (Flow = temporal spine, Blocks = architectural spine) that renders all 96 mechanic nodes from the design spec §4, each with a verbatim model tag and a three-state status chip.

**Architecture:** One shared node inventory (Appendix A below) is the single source of truth for every node's name, model tag, status, Flow group, and Blocks group. Both views assemble their cards from that one inventory, which makes the spec §6 "tag/status byte-identical between views" review check pass by construction. Views are two sibling `<section>` containers toggled by a CSS-only radio switch (no JS, CSP-safe). The current file's CSS vocabulary (starfield, theme vars, `.tier`/`.grid`/`.node`/`.chain`/`.pills`) is carried over and adapted; the four-state chip palette collapses to three states.

**Tech Stack:** Hand-authored HTML5 + inline CSS. No SVG, no external assets, no build step, no JavaScript beyond the (CSS-only) view toggle. Verification is grep/count structural checks plus manual browser eyeballing (no headless tooling in this environment).

## Global Constraints

Copied verbatim from `docs/superpowers/specs/2026-07-12-simulation-flow-artifact-rebuild-design.md`:

- Same file path: `docs/diagrams/generation-flow.html` (full rewrite in place).
- Same living artifact URL `https://claude.ai/code/artifact/67f20b6b-4e8c-4941-b88b-fc071c1c64f4`, the 🌌 favicon, and the `<title>StarGen — Generation &amp; Simulation Flow</title>` — all unchanged. (Republish happens in the parent conversation, not in this plan — see Task 9.)
- Self-contained: no SVG, no external assets/CDN, CSS grid/flexbox only. This file becomes a claude.ai Artifact under a strict CSP, so every byte must be inline.
- No JS beyond a view toggle.
- Three-state status taxonomy, exactly (spec §3): **Implemented** (built, armed, matches design), **Specced-only** (design specifies it, no implementation exists), **Known gap** (implemented but flagged — a stand-in runs, does not yet match spec). No fourth state; the retired "Prototype (PoC)" state does not return.
- Interface connectors between cards are **labeled text**, never drawn SVG edges, never computed coordinates. Every connector label comes from the providing side's Provides column.
- Keep the stamp honest: date `2026-07-12`, current `main` short commit resolved at execution time (`git rev-parse --short HEAD`), status line "A–K roadmap closed; epoch sim implemented; 13 filed gaps + flagged deviations carried."
- **The design tree (`docs/design/`) governs; this page summarizes.** If any node content in this plan conflicts with a `docs/design/` doc, the doc wins and this plan is the bug — flag it to the user rather than shipping the conflict.

---

## Shared Reference — Appendix A: Canonical Node Inventory

**This is the load-bearing artifact.** Every node card in both views is assembled from exactly one row here. The `tag` text is verbatim from spec §4 (reproduced for copy-paste). Assembling the same row into both views is what guarantees byte-identity.

**Column key:**
- `chip` — status chip class: `done` (Implemented, label `IMPL`), `spec` (Specced-only, label `SPEC`), `gap` (Known gap, label `GAP`).
- `flow` — which Flow-view phase card the node renders under, or `—` if the node has no single loop phase. Derived as the **first loop-phase token** in the spec's Phase column (Pc/Mk/Al/In/Rs/Id/Ch). Nodes whose Phase is purely `X`, `Up`, or play-clock get `—`. **`—` does not mean absent from Flow view**: Genesis's 7 `Up`-phase nodes render via the clock-strip/pipeline-chain summary (not as individual `.mech` cards — their content is condensed there); every other `flow = —` node (the L0/Fleet/L4/interfaces/determinism structural nodes) renders as a `.mech` card in the dedicated **Cross-Cutting & Structural** Flow band (`#flow-crosscut`, filled in Task 4) so no node is Blocks-only. Inspection & Rendering nodes are the mirror case: Flow-only, `blocks = —`.
- `blocks` — which Blocks-view group the node renders under.
- `sub` — optional colored sub-line(s) rendered inside the card as `<li class="spec-item">` or `<li class="gap-item">`, carrying the finer flag.

**Chip-selection rule (apply exactly):** chip = `gap` if the spec §4 status cell contains the words "Known gap"; else `spec` if the cell *opens* with "Specced-only"; else `done`. A "Specced-only" sub-aspect inside an otherwise-implemented node does **not** flip the chip — it becomes a `spec-item` sub-line while the chip stays `done`. A "Known gap" mention anywhere flips the chip to `gap` and also gets a `gap-item` sub-line.

### A.1 Genesis — Blocks group `genesis`, all `flow = —`

| id | name | chip | tag (verbatim) |
|---|---|---|---|
| gen1 | Cosmic field stack | done | conserved per-cell fields (Gas, star cohorts, Metals, Remnants); step loop inflow→transport→star-formation→aging→death&enrichment over ~150 deep-time steps |
| gen2 | Cosmic discrete features | done | seeded/emergent registry: mergers (stellar streams), globulars, nebulae, AGN accretion epochs |
| gen3 | Habitability history | done | per-cell scalars: metallicity-floor crossing, last sterilization, stability-since — makes emergence causal |
| gen4 | Biosphere field | done | per-cell LifeViability/BiosphereAge/Richness/SapiencePotential; step loop abiogenesis→aging→catastrophes→spread→sapience-registration |
| gen5 | Emergence schedule | done | each sapient origin's spaceflight date = abiogenesis + richness-scaled maturation; staggered polity entry with late-emerger contact bonus |
| gen6 | Precursor waves | done | coarse civ-arc sim (rise/peak/decline) reusing the space model without markets/characters; vigor classes (grand/pocket); cause-typed endings |
| gen7 | Precursor living residue | done | machine descendants (seed a present machine-species origin), biosphere engineering, sterilization scars, dormant remnants |

### A.2 L0 Substrate — Blocks group `l0`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| l0a | Commodity vocabulary | done | Mk | 17 goods in raw/processed/capital tiers; recipe chains 1–4 nodes deep; standard (exotics-free) vs advanced (exotics-gated) variants, tech-tier-gated | — |
| l0b | Grade system | done | — | every stock = (quantity, grade∈[0,1]); grade flows terrain→chains; `Effective(useCase)=qty×GradeMultiplier`; tech tier is the grade ceiling; precursor grade above any ceiling | — |
| l0c | Demand model | done | Mk | priority bands: population (subsistence/SoL/luxury, embodiment-modulated) · industry · movement (Fuel) · military · technology (Refined Exotics × Compute) | — |
| l0d | Legality schema | done | Mk | per-polity law code legal/restricted/prohibited + tariff; prohibition → black-market demand at margin | — |
| l0e | Sentient trafficking | spec | Mk | illicit population flow against the gradient toward low-rights polities; crime vs the population substrate | spec: unmodeled in both commodities and migration (gap 5) |
| l0f | Infrastructure catalog | done | — | 15 types in 5 families (keystone port + extraction/processing/heavy/support); each tier 1–3, build cost, construction time, upkeep, hex anchor; siting rules | — |
| l0g | Port & domain model | done | — | keystone port: local service radius + gate slots (two growth axes); territory = union of port service areas (derived, never stored); domain overlap = contested zone | — |
| l0h | Lane / gate model | done | Rs | a lane = linked gate pair (one per port system); reach = min gate tier; capacity/speed from tiers; anti-web rule; crossing fees by gate owner | — |
| l0i | Production formula | gap | Mk | `output = base(type,tier) × terrain × labor × machineryGrade × automation(compute)` | gap: automation term — production formula accepts it; Markets passes 0.0 (gap 11) |
| l0j | Organic baseline | done | Mk | unserviced settlements subsistence-farm/craft locally; small enough that facilities dominate | — |
| l0k | Market geography | done | Mk | one market per port at service-area∩lane-network; per-good price/last-cleared-qty/mean-grade + black book; connectivity is price structure; wilds have no market | — |
| l0l | Retail projection | spec | — | play-clock items are retail instances sampling local (good,grade,qty) stocks; tail-sampled exceptional items | spec: play-clock, unbuilt |

### A.3 L1 Economy — Blocks group `l1`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| l1a | Order-book market engine | done | Mk | the market IS the set of open buy/sell orders (EVE model); physical escrow (sells hold goods, buys hold credits); reference price is the persistent readout | — |
| l1b | The market step | done | Mk | fixed 9-step order: expiry sweep → freight sail → requote → supply lands → escrowed demand bids → spread run → matching (price-time priority, MAKER-price fill) → reference drift → clearing consequences | — |
| l1c | Per-owner quote decay | gap | Mk | (design) sold-out sellers raise, glutted cut | gap: "Deviation, flagged" (markets.md §step 3) — discovery lives in reference drift instead |
| l1d | Relay bids | gap | Mk | cheap-end sovereign bids at own reference to stage re-export; hop-by-hop diffusion; entrepôts emerge | gap: "Kept past B2, flagged" (markets.md) — hop diffusion stand-in, retires with multi-hop actor runs |
| l1e | Freight / shipments | done | Mk | a haul = Shipment (origin, dest, cargo, lane route); leg-years priced at departure; blockade/quarantine/dead-gate stalls it; piracy/war-interdiction rolls per sail | — |
| l1f | Spread run | done | Mk | posted fleet's owner trades its lane gradient with own capital: lift cheap asks, sail, post at dear end; absorption reads real resting bids above delivered break-even | — |
| l1g | Courier contracts | gap | Mk | internal logistics as a market: courier posts (origin, dest, escrowed cargo+fee); board clears (priority, id); War priority outbids commerce; requisition channel rides it | gap: ranking deviation (markets.md §courier) — ranks (priority, id), fee prices poster cost only |
| l1h | Household income & labor share | done | Mk | facilities pay a labor share of revenue to staffing segments (shrinks with automation); SoL derives from real wages at local prices | — |
| l1i | Wealth & taxation | done | Mk | transaction tax on sales + tariffs on cross-border freight + state-facility income; true wealth = ledger + asset book (emergent readout) | — |
| l1j | Credit / loans | gap | Al | loan objects (lender, borrower, principal, rate, term); default → reputation/relations hit, collateral seizure; no banks, lenders are whoever holds surplus | gap: structural debt spiral (HANDOFF/SH); LoanRatePerYear a dead knob; 2×-lender gate kills the credit market epochs 1–4 |
| l1k | Stockpiles & procurement | gap | Mk | stock has an address (resting order / larder / laydown yard / in-transit); per-port procurement toward standing targets from reserve treasury; depot tiers bank & slow decay; local siege buffering | gap: perishability — design says decay compounds; reserves store loss-free (gap 6); gap: procurement contract objects — escrowed contracts stand in as mechanical stockpile-target procurement (gap 3) |
| l1l | Interdiction strain | done | Mk | per-lane realized-vs-potential trade value minus smuggling leakage; measured where it happens | — |
| l1m | Sanctions & tariffs | done | Mk | tariff schedule collected at the entered gate; sanction = non-war lane-legality closure; both evadable at margin, both feed trade→relations hook | spec: sanctions closure — lane-legality closure machinery absent (gap 4) |
| l1n | Construction / projects | done | Al | every in-flight work = a project with a rate contract: BuildCost ÷ ConstructionYears per-year basket + wages; bids as a market participant into a laydown yard; advances by scarcest-input fraction; priority-ordered feeds; abandon clock | — |
| l1o | Plan packing | done | In | standing plan packed against real capability: income/yr + savings drawdown; colony batches boosted to front when expansion points sit hull-less | — |
| l1p | Condition & ownership | done | Al | facilities carry condition (decays w/o upkeep, war-damaged, repaired); ownership transfers by sale/seizure/nationalization/conquest, each a conserved ledger event | — |
| l1q | Technology | done | Al | 4 per-polity domains (Industrial/Military/Astrogation/Life); geometric tier ladders unlock ceilings/regions; research consumes Refined Exotics × Compute in Allocation; diffusion via trade/salvage/espionage | spec: espionage diffusion channel reserved by design (gap 13) |

### A.4 L1 Corporations & outlaws — Blocks group `corp`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| co1 | Corporate founding | done | Id | simulation watches persistent profit niches (price gradient / unexploited deposit / unserved route) over consecutive epochs; charter event via graduation; niche stamps character | — |
| co2 | Corporate controller | gap | In | standing plan (polity planner machinery at corp scope) packed against income + savings drawdown; dividend rate; lobby targets; risk appetite | gap: plan scope — "Scoped, flagged" (corporations.md §controller): plans cover facilities; routes/gates opportunistic, hulls immediate |
| co3 | Portfolio & operations | done | Mk | owns facilities/freighters/depots/routes across borders; speculation is the business (spread runs on own capital); internal logistics on courier contracts; vertical integration | — |
| co4 | Corporate influence | done | Id | lobby spending strengthens aligned factions (dividends → elite faction wealth); sanction evasion by re-flagging through subsidiaries | — |
| co5 | Corporate death & estates | done | Id | bankruptcy (default cascade) / nationalization / niche death; estates-pass settles orders, cargo, jobs, projects, credits, debt conservatively | — |
| co6 | Outlaw institutions | gap | Id | same niche rule founds cartels (black-book) & pirate bands (raiding niche = lawlessness × cargo value); based at ruin/haven POIs | gap: piracy-risk-pricing not priced into freight profit (gap 10) |

### A.5 Fleet model (cross-cutting) — Blocks group `fleet`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| fl1 | Chassis grid | gap | — | design = role × size cell (Freight/Escort/Line/Carrier/Scout/Colony/Special); instantiated per polity from embodiment × culture × tech × grade | gap: Carrier role unused (gap 10) |
| fl2 | Design sheet | gap | — | two-layer stat model: ~15-stat sheet (Combat/Mobility/Capacity/Operations) + epoch aggregation into vectors; grade/tech act per-stat; refit variants | gap: refit variants unused (gap 10) |
| fl3 | Design lineages | done | — | designs drift along named lineages/marks over epochs — fleet composition reads as cultural history | — |
| fl4 | Hull production | done | Al | shipyards convert Ship Components (+Armaments/+Compute) into hulls; hull-batch project anchored at a yard; yard tier caps concurrent batch work | — |
| fl5 | Fleet object & postures | done | Mk | `(id, owner, location, composition, posture, commander, supply)`; postures Posted/Escort/Patrol/Blockade/Expedition-Convoy/Reserve | — |
| fl6 | Movement & supply | done | Rs | three leg types (intra-domain/lane-hop/off-lane); off-lane gated on endurance floor; fleets draw fuel/upkeep from nearest owned port; unsupplied lose readiness then hulls | — |
| fl7 | Attrition & wreckage | gap | Rs | losses conserve into wreckage at the death hex → salvage sites & battlefield POIs; piracy risk/lane = lawlessness × cargo value − escort vectors | gap: piracy-risk-into-profit not modeled (gap 10) |
| fl8 | Information carriage | done | Pc | news speed/lane = f(posted traffic frequency); courier/scout fleets are deliberate info assets; player carrying news is this at individual scale | spec: courier fast-paths — news travels traffic only; couriers/scouts/news-carrying player are play-clock (gap 12) |
| fl9 | Commanders | gap | In | fleets above a threshold take a commander role; personality biases posture AI; renown accrues; age/die/succeed/defect | gap: boldness bias missing (gap 9) |

### A.6 L2 Polity interior — Blocks group `l2`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| p1 | Population segments | done | Id | `(species, culture, size, SoL, ideology distribution)`; domain-level state (hex is a projection); conserved, identity travels; mixed by conquest/migration/diaspora | — |
| p2 | Demographics | done | Id | growth = f(SoL, provisions, embodiment); machine populations grow by manufacture (Machinery+Compute), age out when cut off; famine/war shrink | — |
| p3 | Culture | gap | Id | registry entities, species-rooted, syllable flavor names systems/ships/characters; spread by migration/conquest | gap: drift — mint-at-schism works; separation-split & slow blending undone (gap 7) |
| p4 | Ideology | done | Id | 4 axes (Authority↔Autonomy · Communal↔Individual · Open↔Insular · Sacral↔Material); segment distributions drift with lived conditions; official = weighted opinion × institutional inertia | — |
| p5 | Migration | done | Id | per-step segment flows along SoL/safety/affinity/opportunity gradients × distance/lane access; refugees (fast) & diasporas (memory-carrying minorities) | spec: trafficking — illicit flow unmodeled (gap 5) |
| p6 | Faction formation | done | Id | coalesces when a coherent interest diverges from rule; six bases (ideological/cultural/regional/corporate/military/sacral); state = basis, strength, agenda, militancy | — |
| p7 | Faction pressure | done | Id | policies drift toward strong factions (bounded); appeasement spending buys off; unappeased accumulate grievance | — |
| p8 | Graduation | done | Id | `strength × grievance > legitimacy × enforcement` → Schism (polity) / Coup (ruler, → civil war) / Charter (corp) / Revolt (failed) | — |
| p9 | Legitimacy & cohesion | done | Id | legitimacy = f(SoL trend, ideology gap, war outcomes, prestige, cultural accommodation); cohesion = aggregate × structural strain; low cohesion lowers graduation thresholds | — |
| p10 | Government forms | done | Id | closed catalog of 8 (Autocracy/Collective/Assembly/Syndicate/Theocracy/Hive Unity/Machine Consensus/Steward Dynasty) seated in ideology × species; sets succession, inertia, faction tolerance; changes through graduation | — |
| p11 | Temperament composition | done | In | Intent-AI personality = species disposition × official ideology × ruler personality × faction pressure, weighted by government form | — |
| p12 | Characters | done | Id | sparse; generated on demand from (institution, culture, species, seed); personality = ideology position + boldness/zeal/competence/ambition; lifespan/succession; dynasties; notables (hero/founder/prophet/pirate-lord/magnate/explorer); derivable biography | spec: personal acts — 11 unarmed contract acts unbuilt (gap 1) |
| p13 | Plagues | gap | Id | outbreak ∝ density × SoL deficit × exposure; propagates the lane graph with traffic; Medicine mitigation; quarantine = self-imposed interdiction; burns out; memorial residue | gap: depth — excavation-release, Medicine mitigation, memorial POIs, era-signature missing (gap 8) |

### A.7 L3 Inter-polity — Blocks group `l3`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| i1 | Contact | done | Id | polities meet when reach overlaps (expansion/trade/news); first contact composes stance from temperament × strangeness × pre-arrived reputation | — |
| i2 | Native policy | done | In | on covering a pre-spaceflight homeworld: protectorate / integrate / exploit / uplift (Intent act, ideology-weighted, reputation-bearing) | — |
| i3 | Late-emergence resolution | done | Id | emergence in free space = new polity; inside claimed space resolves by host native policy (vassal/autonomous member/suppressed + liberation casus belli) | — |
| i4 | Expansion prices neighbors | done | In | colony valuation discounts a site per entangled foreign domain; founding costs instant tension with each; borders contiguous by choice | — |
| i5 | Relations state | done | Id | per-pair warmth (interdependence) + tension (friction, the war-pressure gauge, decays only when sources resolve); treaty rungs (trade pact / non-aggression / defense alliance / federation-vassalage) at mutual consent | — |
| i6 | Federation | done | Rs | merge gate (sustained alliance + warmth + ideology compat + openness + cohesion); entangled friendly borders push to fusion; fused polity is NEW (weighted composition, fresh form) | — |
| i7 | Vassalage | done | Rs | asymmetric rung: imposed by settlement or chosen under threat; tribute/defense/policy-lock; exits absorption (drift) & secession (bid) | — |
| i8 | Dynastic instruments | gap | Rs | marriages/wardships buy warmth & create succession claims (tension pointed the other way); rare personal unions = federation fast-path | gap: personal unions missing (gap 9) |
| i9 | War causes | done | In | tension discharges through a casus belli menu (economic/ideological/political/spatial/spark); spark rolls in high-friction space; aims scale with hatred → annihilation when saturated | — |
| i10 | War conduct | gap | Rs | theater/objective model: assignment per doctrine+commander → per-objective engagement on fleet vectors (fortification, supply, competence, rolls) → sieges (reserves, fortress tier, relief); mobilization is a ramp not a switch | gap: depth — occupation objectives, defensive mirrors, raidable supply objectives missing (gap 9) |
| i11 | Front supply lines | done | Rs | war force draws upkeep from nearest owned port (forward depot); quartermaster stocks it via War-priority couriers; war interdiction rolls seizure per contested sail, escorts damp deterministically; starvation bites readiness | — |
| i12 | Allied belligerents | done | Rs | defense-alliance partners join as supporters under war leaders; settlements negotiated between leaders; allied gains/grievances flow through the leader's table | — |
| i13 | Termination & settlement | gap | Rs | break on political collapse / exhaustion / capital loss / extinction; settlement negotiated from per-objective outcomes (cede/reparations/vassalize/imposed legality/white peace); accept when perceived cost > settlement (stale, so wars overrun) | gap: imposed-legality settlement missing (gap 9) |
| i14 | War aftermath | done | Ch | grudges → standing claims (tomorrow's tension); veterans → military factions; heroes mint; wreckage/razed → POIs; conduct reputation travels the news graph | — |

### A.8 L4 Narrative — Blocks group `l4`

| id | name | chip | flow | tag (verbatim) | sub |
|---|---|---|---|---|---|
| n1 | News pulses | done | Ch | Chronicle emits pulses for public events above magnitude floors; travel the lane graph at traffic-derived speed, attenuating with distance; couriers/scouts/player are fast paths | spec: fast paths — couriers/scouts/player unbuilt (gap 12) |
| n2 | Perception state | done | Pc | per-actor compressed beliefs: stance table + belief snapshots per (observer, subject) frozen at last refresh (refresh when elapsed years cover news delay); self-facts fresh; corporate perceives own capability | spec: perceived-price arbitrage — belief carries no partner prices (gap 2) |
| n3 | Stances & reputation | done | Pc | news arrival updates stance filtered through observer temperament; reputation derived (never stored) as per-audience stance aggregates, consumed as gates | — |
| n4 | Event grammar | done | Ch | one schema across four clocks: `(id, world-year, clock stratum, type, actors[], location, magnitude, valence, visibility, payload)`; 8 type families; visibility public/regional/secret; indexes are views | — |
| n5 | Chronicle views | done | Ch | queries over the one log at every zoom: galaxy (era detection clusters epochs by signature) / polity (reign arc) / character (biography) / place (hex annotation & archaeology) | — |
| n6 | POI compiler | done | Ch | runs inside Chronicle every epoch, converting qualifying events into anchored POIs immediately (battlefields→salvage, ruins→lawlessness, razed capitals→claim anchors, memorials, precursor sites); live sim effects; one-anchor-per-hex by magnitude; decay as consumed | — |
| n7 | World-state handoff | done | — | final artifact layer: complete registries + deliberately open threads; resumability (same machine at play tick); controller handover; log never closes; delta boundary (save = config + artifact + deltas + log continuation) | — |

### A.9 Cross-cutting interfaces strip — Blocks group `interfaces` (all `flow = —`)

| id | name | chip | tag (verbatim) | sub |
|---|---|---|---|---|
| x1 | Controller contract | done | `Decide(perceivedState) → (policies, acts)` per actor kind (polity/corp/character); the Intent-phase API = the player UI surface; enumerated in `frame/controller-contract.md` | spec: 11 unarmed acts (gap 1); armed: found-colony, declare-war, treaty, settlement-response, nationalize, vassalage, dynastic instrument, quarantine |
| x2 | Price signal | done | market-price-derived valuations in grade-effective units are the one value language: expansion attractiveness, war-goal value, migration pull, investment, siting all read it | — |
| x3 | Event grammar (interface) | done | every subsystem emits one schema; L4 owns it; emitting well-formed history is a requirement on every mechanic | — |
| x4 | Pressure → graduation | done | L2 faction machinery is the sole factory for new institutions; L3 consumes schisms, L1 consumes charters; emergence schedule is the one non-faction origin | — |

### A.10 Determinism & artifact discipline — Blocks group `determinism` (all `flow = —`)

| id | name | chip | tag (verbatim) |
|---|---|---|---|
| d1 | Determinism discipline | done | stateless hash rolls keyed (step, actor id, channel 0–73); fixed iteration order; config artifact-stamped; byte-identity at coarse & fine ticks |
| d2 | Artifact layers | done | ~22 versioned registry layers (ports, lanes, facilities, designs, fleets, wreckage, segments, markets, loans, characters, dynasties, factions, corporations, relations, wars, beliefs, pulses, POIs, plagues…); delta saves = base + changed layers + log continuation; hex tier never persisted |
| d3 | Four-clock rate model | done | all rates in world-years (P7); epoch = 25y integration step, not a unit; durations are world-time state; coarse/fine sample the same durations |

### A.11 Inspection & Rendering — Flow-view band `inspection` only (all `blocks = —`)

| id | name | chip | tag (verbatim) | sub |
|---|---|---|---|---|
| r1 | Inspector REPL | done | epoch run/save/load (22-layer), watch, emap layers, panels, threads, estep (fine tick), delta saves, belief/news/era/poi queries | — |
| r2 | Unity atlas | done | rebuilt against the settled model (K1–K5 merged): skeleton → lenses → panels → timeline → system stage; five LOD bands; map/system-stage lenses; typed pickables → panels | note: was PoC — key staleness fix |
| r3 | Sim-health harness | done | MetricRegistry + per-phase MoneyRow; conservation residual ≈1e-8; ensemble sweep runner; `ehealth`; in-memory only, never serialized | — |
| r4 | Game layer / play clock | spec | live game: player verbs, perceived-price trading, news-carrying; the acceptance gap list | spec: future |

### Tally (drives the count checks)

- Distinct nodes: 96 (7 genesis + 12 L0 + 17 L1 + 6 corp + 9 fleet + 13 L2 + 14 L3 + 7 L4 + 4 interfaces + 3 determinism + 4 inspection).
- Flow-view mech cards: nodes with a phase (`flow ≠ —`) = **70**, plus the 4 inspection band cards, plus the **15-node Cross-Cutting & Structural band** (`#flow-crosscut`: l0b, l0f, l0g, l0l, fl1, fl2, fl3, n7, x1, x2, x3, x4, d1, d2, d3 — every `flow = —` node except the 7 Genesis ones, which condense into the clock-strip/pipeline instead) = 70 + 4 + 15 = **89**.
- Blocks-view mech cards: every node with `blocks ≠ —` = 96 − 4 inspection = **92**.
- Total `<div class="node mech ...">` elements in the file = 89 + 92 = **181**.
- 17 distinct `gap` nodes, 3 distinct `spec` nodes (l0e, l0l, r4), 76 distinct `done` nodes. Rendered chip *count* (not distinct nodes) depends on each node's view count — see Task 8 Step 2, which now also accounts for fl1/fl2/fl3/n7/x1–x4/d1–d3/l0b/l0f/l0g/l0l becoming both-view via the crosscut band (previously Blocks-only).

### Flow-view phase rosters (assembled from the `flow` column)

- **Perception (Pc):** n2, n3, fl8
- **Markets (Mk):** l0a, l0c, l0d, l0e, l0i, l0j, l0k, l1a, l1b, l1c, l1d, l1e, l1f, l1g, l1h, l1i, l1k, l1l, l1m, co3, fl5
- **Allocation (Al):** l1j, l1n, l1p, l1q, fl4
- **Intent (In):** l1o, co2, p11, fl9, i2, i4, i9
- **Resolution (Rs):** l0h, fl6, fl7, i6, i7, i8, i10, i11, i12, i13
- **Interior & Demographics (Id):** co1, co4, co5, co6, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p12, p13, i1, i3, i5
- **Chronicle (Ch):** n1, n4, n5, n6, i14

(Perception 3 + Markets 21 + Allocation 5 + Intent 7 + Resolution 10 + Interior 19 + Chronicle 5 = 70. ✓)

- **Cross-Cutting & Structural band (`#flow-crosscut`):** l0b, l0f, l0g, l0l, fl1, fl2, fl3, n7, x1, x2, x3, x4, d1, d2, d3 (15 nodes — every `flow = —` node except Genesis).

---

## Task 1: Page shell, CSS system, view toggle, header/footer scaffold

**Files:**
- Rewrite: `docs/diagrams/generation-flow.html`

**Interfaces:**
- Produces: the `<title>`; the three-state CSS classes `.node.done`/`.node.spec`/`.node.gap`, `.chip.done`/`.chip.spec`/`.chip.gap`, `li.spec-item`/`li.gap-item`; the base structural classes `.wrap`, `.tier`, `.tier-head`, `.tier-tag`, `.grid`, `.node`, `.node.mech`, `.node-head`, `.name`, `.chip`, `.tag`, `.subs`, `.chain-scroll`, `.chain`, `.chain .node.clock`, `.chain .link`, `.pills`, `.pill`, `.arrow`, `.subgroup`, `.opr`; the CSS-only toggle inputs `#tab-flow` / `#tab-blocks` and the two empty view containers `<section id="view-flow">` / `<section id="view-blocks">` inside `.views`. Every later task appends children into one of those two sections.

- [ ] **Step 1: Write the structural check (expect fail on the current stale file)**

Run (Bash tool):
```bash
grep -c 'id="view-flow"' docs/diagrams/generation-flow.html; \
grep -c 'id="view-blocks"' docs/diagrams/generation-flow.html; \
grep -c 'id="tab-flow"' docs/diagrams/generation-flow.html; \
grep -c 'class="node.*gap"' docs/diagrams/generation-flow.html
```
Expected on the current file: `0`, `0`, `0`, `0` (none of the new scaffolding exists yet).

- [ ] **Step 2: Write the file shell**

Replace the entire file with the following. (The tags start at `<title>` — the `<!doctype>/<head>/<body>` wrapper is added by the Artifact publisher, matching the current file's convention.)

```html
<title>StarGen — Generation &amp; Simulation Flow</title>
<style>
  :root {
    --bg: #F4F6FA; --panel: #FFFFFF; --panel2: #EDF0F6;
    --ink: #1C2434; --muted: #5A6478; --line: #C9D0DE; --accent: #3E6FD9;
    --done: #1F8A63; --done-bg: rgba(31,138,99,.09);  --done-line: rgba(31,138,99,.45);
    --spec: #A06E12; --spec-bg: rgba(176,120,24,.10); --spec-line: rgba(176,120,24,.5);
    --gap:  #C2410C; --gap-bg:  rgba(194,65,12,.09);  --gap-line:  rgba(194,65,12,.5);
    --star-opacity: 0.25;
    --mono: "Cascadia Code", "Cascadia Mono", ui-monospace, "SF Mono", Consolas, monospace;
    --sans: "Segoe UI", system-ui, -apple-system, sans-serif;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --bg: #0B0E16; --panel: #131826; --panel2: #0F1420;
      --ink: #E6EAF5; --muted: #98A0B5; --line: #2A3247; --accent: #7FA3F0;
      --done: #4CC79A; --done-bg: rgba(76,199,154,.08); --done-line: rgba(76,199,154,.4);
      --spec: #E0AF52; --spec-bg: rgba(224,175,82,.08); --spec-line: rgba(224,175,82,.45);
      --gap:  #F0A868; --gap-bg:  rgba(240,168,104,.09);--gap-line:  rgba(240,168,104,.45);
      --star-opacity: 1;
    }
  }
  :root[data-theme="light"] {
    --bg: #F4F6FA; --panel: #FFFFFF; --panel2: #EDF0F6;
    --ink: #1C2434; --muted: #5A6478; --line: #C9D0DE; --accent: #3E6FD9;
    --done: #1F8A63; --done-bg: rgba(31,138,99,.09); --done-line: rgba(31,138,99,.45);
    --spec: #A06E12; --spec-bg: rgba(176,120,24,.10); --spec-line: rgba(176,120,24,.5);
    --gap:  #C2410C; --gap-bg:  rgba(194,65,12,.09);  --gap-line:  rgba(194,65,12,.5);
    --star-opacity: 0.25;
  }
  :root[data-theme="dark"] {
    --bg: #0B0E16; --panel: #131826; --panel2: #0F1420;
    --ink: #E6EAF5; --muted: #98A0B5; --line: #2A3247; --accent: #7FA3F0;
    --done: #4CC79A; --done-bg: rgba(76,199,154,.08); --done-line: rgba(76,199,154,.4);
    --spec: #E0AF52; --spec-bg: rgba(224,175,82,.08); --spec-line: rgba(224,175,82,.45);
    --gap:  #F0A868; --gap-bg:  rgba(240,168,104,.09); --gap-line: rgba(240,168,104,.45);
    --star-opacity: 1;
  }

  * { box-sizing: border-box; }
  body {
    background: var(--bg); color: var(--ink);
    font-family: var(--sans); font-size: 14px; line-height: 1.5;
    margin: 0; padding: 2.5rem 1.25rem 4rem; position: relative;
  }
  body::before {
    content: ""; position: fixed; inset: 0; pointer-events: none; z-index: 0;
    opacity: var(--star-opacity);
    background-image:
      radial-gradient(1px 1px at 12% 18%, rgba(160,180,230,.5) 50%, transparent 51%),
      radial-gradient(1px 1px at 78% 8%,  rgba(160,180,230,.4) 50%, transparent 51%),
      radial-gradient(1.5px 1.5px at 55% 32%, rgba(200,215,255,.35) 50%, transparent 51%),
      radial-gradient(1px 1px at 30% 58%, rgba(160,180,230,.4) 50%, transparent 51%),
      radial-gradient(1px 1px at 88% 47%, rgba(160,180,230,.5) 50%, transparent 51%),
      radial-gradient(1.5px 1.5px at 8% 82%, rgba(200,215,255,.3) 50%, transparent 51%),
      radial-gradient(1px 1px at 65% 74%, rgba(160,180,230,.4) 50%, transparent 51%),
      radial-gradient(1px 1px at 42% 92%, rgba(160,180,230,.35) 50%, transparent 51%);
  }
  .wrap { max-width: 64rem; margin: 0 auto; position: relative; z-index: 1; }

  header { margin-bottom: 1.5rem; }
  .eyebrow { font-family: var(--mono); font-size: .7rem; letter-spacing: .18em;
    text-transform: uppercase; color: var(--accent); margin: 0 0 .5rem; }
  h1 { font-family: var(--mono); font-size: 1.45rem; font-weight: 600; margin: 0 0 .5rem; text-wrap: balance; }
  .subtitle { color: var(--muted); max-width: 48rem; margin: 0 0 1.25rem; }

  .legend {
    display: flex; flex-wrap: wrap; gap: .6rem 1.4rem; align-items: center;
    font-family: var(--mono); font-size: .72rem;
    border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); padding: .6rem 0;
  }
  .legend .key { display: flex; align-items: center; gap: .45rem; }
  .dot { width: .55rem; height: .55rem; border-radius: 50%; flex: none; }
  .dot.done { background: var(--done); }
  .dot.spec { background: var(--spec); }
  .dot.gap  { background: transparent; border: 1.5px dashed var(--gap); }
  .legend .stamp { margin-left: auto; color: var(--muted); }

  /* ---- CSS-only view toggle (no JS) ---- */
  #tab-flow, #tab-blocks { position: absolute; opacity: 0; pointer-events: none; }
  .toggle { display: inline-flex; gap: .25rem; margin: 1.25rem 0 1.75rem;
    border: 1px solid var(--line); border-radius: 999px; padding: .25rem; background: var(--panel2); }
  .toggle label {
    font-family: var(--mono); font-size: .78rem; cursor: pointer; user-select: none;
    padding: .35rem 1.1rem; border-radius: 999px; color: var(--muted); border: 1px solid transparent;
  }
  #tab-flow:checked  ~ .toggle label[for="tab-flow"],
  #tab-blocks:checked ~ .toggle label[for="tab-blocks"] {
    background: var(--panel); color: var(--ink); border-color: var(--line);
  }
  .views > section { display: none; }
  #tab-flow:checked   ~ .views > #view-flow   { display: block; }
  #tab-blocks:checked ~ .views > #view-blocks { display: block; }

  /* ---- tier bands ---- */
  .tier { border: 1px solid var(--line); background: var(--panel2);
    border-radius: 3px; padding: 1.1rem 1.1rem 1.25rem; margin: 0; }
  .tier-head { display: flex; align-items: baseline; gap: .75rem; flex-wrap: wrap; margin-bottom: .35rem; }
  .tier-tag { font-family: var(--mono); font-size: .68rem; letter-spacing: .14em;
    text-transform: uppercase; color: var(--accent);
    border: 1px solid var(--accent); border-radius: 2px; padding: .1rem .45rem; }
  .tier-head h2 { font-family: var(--mono); font-size: 1.02rem; font-weight: 600; margin: 0; }
  .tier-head .nature { font-size: .78rem; color: var(--muted); }
  .tier > .desc { color: var(--muted); font-size: .82rem; margin: 0 0 .9rem; max-width: 54rem; }
  .opr { font-family: var(--mono); font-size: .72rem; color: var(--muted);
    margin: 0 0 .9rem; display: grid; gap: .2rem; }
  .opr b { color: var(--ink); font-weight: 600; }

  /* ---- connector arrows ---- */
  .arrow { align-self: center; display: flex; flex-direction: column; align-items: center;
    padding: .15rem 0; color: var(--accent); }
  .arrow .stem { width: 1px; height: 1.1rem; background: var(--accent); opacity: .7; }
  .arrow .head { font-size: .7rem; line-height: .6; margin-top: -1px; }
  .arrow .lbl { font-family: var(--mono); font-size: .68rem; color: var(--muted); padding: .1rem 0 .2rem; text-align: center; }

  /* ---- node cards ---- */
  .grid { display: grid; gap: .7rem; grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); }
  .node { background: var(--panel); border: 1px solid var(--line); border-radius: 3px;
    padding: .7rem .85rem .75rem; min-width: 0; }
  .node.done { border-color: var(--done-line); background: linear-gradient(var(--done-bg), var(--done-bg)), var(--panel); }
  .node.spec { border-color: var(--spec-line); background: linear-gradient(var(--spec-bg), var(--spec-bg)), var(--panel); }
  .node.gap  { border-style: dashed; border-color: var(--gap-line); background: linear-gradient(var(--gap-bg), var(--gap-bg)), var(--panel); }
  .node-head { display: flex; align-items: baseline; gap: .6rem; margin-bottom: .25rem; }
  .node-head .name { font-family: var(--mono); font-size: .83rem; font-weight: 600; }
  .chip { font-family: var(--mono); font-size: .55rem; letter-spacing: .12em;
    padding: .08rem .4rem; border-radius: 2px; flex: none; margin-left: auto; }
  .chip.done { color: var(--done); border: 1px solid var(--done-line); }
  .chip.spec { color: var(--spec); border: 1px solid var(--spec-line); }
  .chip.gap  { color: var(--gap);  border: 1px dashed var(--gap-line); }
  .node .tag { margin: 0; font-size: .76rem; color: var(--muted); }
  .node ul.subs { margin: .4rem 0 0; padding-left: 1rem; font-size: .74rem; }
  .node ul.subs li { margin: .12rem 0; }
  li.spec-item { color: var(--spec); } li.spec-item::marker { color: var(--spec); }
  li.gap-item  { color: var(--gap); }  li.gap-item::marker  { color: var(--gap); }

  /* ---- sub-groups ---- */
  .subgroup { margin-top: 1rem; }
  .subgroup > h3 { font-family: var(--mono); font-size: .72rem; letter-spacing: .14em;
    text-transform: uppercase; color: var(--muted); margin: 0 0 .5rem;
    display: flex; align-items: center; gap: .6rem; }
  .subgroup > h3::after { content: ""; flex: 1; height: 1px; background: var(--line); }
  .stack { display: flex; flex-direction: column; }
  .stack > .tier { border-radius: 3px; }

  /* ---- horizontal chains ---- */
  .chain-scroll { overflow-x: auto; padding-bottom: .35rem; }
  .chain { display: flex; align-items: stretch; gap: 0; min-width: max-content; }
  .chain .node { width: 13.5rem; flex: none; }
  .chain .node.clock { width: 12.5rem; }
  .chain .link { align-self: center; color: var(--accent); font-family: var(--mono);
    padding: 0 .35rem; font-size: .85rem; flex: none; }
  .pills { display: flex; align-items: center; gap: .3rem; min-width: max-content; }
  .pill { font-family: var(--mono); font-size: .72rem; flex: none;
    border: 1px solid var(--done-line); color: var(--ink);
    background: var(--panel); border-radius: 999px; padding: .22rem .65rem; }
  .pills .link { color: var(--accent); font-size: .75rem; }

  footer { margin-top: 2.2rem; border-top: 1px solid var(--line); padding-top: 1rem;
    font-size: .76rem; color: var(--muted); }
  footer .src { font-family: var(--mono); font-size: .7rem; }
  footer ul { margin: .35rem 0 0; padding-left: 1rem; }
  footer li { margin: .1rem 0; }
  a { color: var(--accent); }
  @media (prefers-reduced-motion: no-preference) { .node { transition: border-color .15s ease; } }
</style>

<div class="wrap">
  <header>
    <p class="eyebrow">StarSystemGeneration · living baseline</p>
    <h1>Generation &amp; Simulation Flow</h1>
    <p class="subtitle">
      The whole simulation, two ways at once. <strong>Flow</strong> is the temporal spine — the
      four clocks, the hex-generation pipeline, and the seven-phase epoch loop. <strong>Blocks</strong>
      is the architectural spine — Genesis feeding five levels (L0 Substrate → L4 Narrative), the
      fleet &amp; actor cross-cutting models, and the frame interfaces. Every mechanic is one node with
      its actual model and a status; both views draw from one shared inventory.
    </p>
    <div class="legend">
      <span class="key"><span class="dot done"></span> Implemented — built, armed, matches design</span>
      <span class="key"><span class="dot spec"></span> Specced-only — designed, no implementation exists</span>
      <span class="key"><span class="dot gap"></span> Known gap — implemented but flagged (stand-in running)</span>
      <span class="stamp">2026-07-12 · COMMIT · A–K roadmap closed; epoch sim implemented; 13 filed gaps + flagged deviations carried</span>
    </div>
  </header>

  <input type="radio" name="view" id="tab-flow" checked>
  <input type="radio" name="view" id="tab-blocks">
  <div class="toggle">
    <label for="tab-flow">Flow · temporal spine</label>
    <label for="tab-blocks">Blocks · architecture</label>
  </div>

  <div class="views">
    <section id="view-flow" class="stack"></section>
    <section id="view-blocks" class="stack"></section>
  </div>

  <footer>
    <span class="src">Sources of truth (the design tree governs; this page summarizes):</span>
    <ul>
      <li><span class="src">docs/design/</span> — the living systems design (start at <span class="src">docs/design/README.md</span>): frame (principles, actors, clocks, simulation-flow, system-map, controller-contract) + one doc per subsystem (genesis, substrate, economy, corporations, fleets, polity, interpolity, narrative).</li>
      <li><span class="src">docs/superpowers/specs/2026-07-12-simulation-flow-artifact-rebuild-design.md</span> — the content brief this artifact renders.</li>
      <li><span class="src">docs/superpowers/specs/2026-07-11-design-acceptance.md</span> — the P1–P8 acceptance pass + the 13-item gap list cited by the Known-gap nodes.</li>
      <li><span class="src">docs/HANDOFF.md</span> — current session state, carried/flagged items.</li>
    </ul>
  </footer>
</div>
```

- [ ] **Step 3: Re-run the structural check (expect pass)**

Run:
```bash
grep -c 'id="view-flow"' docs/diagrams/generation-flow.html; \
grep -c 'id="view-blocks"' docs/diagrams/generation-flow.html; \
grep -c 'id="tab-flow"' docs/diagrams/generation-flow.html; \
grep -c 'class="dot gap"' docs/diagrams/generation-flow.html
```
Expected: `1`, `1`, `1`, `1`.

- [ ] **Step 4: Visual verification**

Open the file in a browser: PowerShell `Invoke-Item docs\diagrams\generation-flow.html` (or the `run` skill, or `start docs/diagrams/generation-flow.html`). No headless tooling is assumed — eyeball it directly. Confirm: header + subtitle render; the three-key legend shows three visually distinct swatches (solid green dot, solid gold dot, dashed orange ring); the toggle shows two pill labels; clicking "Blocks · architecture" makes the (empty) page body swap containers with no console error; the starfield shows in dark mode. Both view sections are empty for now — that is expected.

- [ ] **Step 5: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): scaffold two-view flow artifact — shell, 3-state CSS, CSS-only toggle"
```

---

## Task 2: Flow view — clocks strip, upstream pipeline, seven phase-card shells, Inspection band shell

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (fill `<section id="view-flow">`)

**Interfaces:**
- Consumes: `#view-flow`, `.tier`, `.chain`, `.chain .node.clock`, `.pills`, `.pill`, `.arrow`, `.subgroup`, `.grid` from Task 1.
- Produces: seven phase containers with stable ids `#phase-pc`, `#phase-mk`, `#phase-al`, `#phase-in`, `#phase-rs`, `#phase-id`, `#phase-ch` (each holding a `.grid` for its mech cards), `#flow-crosscut` holding a `.grid`, and `#flow-inspection` holding a `.grid`. Tasks 3–4 append `.node.mech` cards into these grids.

- [ ] **Step 1: Structural check (expect fail)**

Run: `grep -c 'id="phase-mk"' docs/diagrams/generation-flow.html`
Expected: `0`.

- [ ] **Step 2: Fill the Flow section**

Replace `<section id="view-flow" class="stack"></section>` with the block below. (Phase grids are intentionally empty here; Tasks 3–4 fill them.)

```html
<section id="view-flow" class="stack">

  <section class="tier">
    <div class="tier-head"><span class="tier-tag">Frame</span><h2>The Four Clocks</h2>
      <span class="nature">each hands the next a finished board + latent story material (frame/time.md)</span></div>
    <div class="chain-scroll"><div class="chain">
      <div class="node done clock"><div class="node-head"><span class="name">Cosmic</span><span class="chip done">IMPL</span></div>
        <p class="tag">~14 Gyr, ~150 steps. Field stack (gas, star cohorts, metals, remnants) + discrete features. <strong>→ the physical galaxy</strong></p></div>
      <span class="link">→</span>
      <div class="node done clock"><div class="node-head"><span class="name">Evolutionary</span><span class="chip done">IMPL</span></div>
        <p class="tag">~Gyrs, ~Myr steps. Biosphere field → emergence schedule; precursor civ-arc waves → archaeology + living residue. <strong>→ the living galaxy</strong></p></div>
      <span class="link">→</span>
      <div class="node done clock"><div class="node-head"><span class="name">Generational</span><span class="chip done">IMPL</span></div>
        <p class="tag">~1,000y in ~25y epochs (a reign ≈ 1–3 epochs). Polities, economies, wars, culture — the epoch loop below. <strong>→ world-state handoff</strong></p></div>
      <span class="link">→</span>
      <div class="node spec clock"><div class="node-head"><span class="name">Play</span><span class="chip spec">SPEC</span></div>
        <p class="tag">Days–weeks per tick. Same rules, finer sampling; news at ship speed; the player assumes any controller slot.</p></div>
    </div></div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">cosmic + evolutionary outputs seed the hex tier</span></div>

  <section class="tier">
    <div class="tier-head"><span class="tier-tag">Upstream</span><h2>Hex-Generation Pipeline</h2>
      <span class="nature">pure fn · per-hex · never persisted</span></div>
    <p class="desc">The Phase-1 generation spine: identity + tuning resolve into a density field, a persisted skeleton, and — on demand — a hex. <strong>hex = f(GalaxyConfig, coordinate)</strong>; the skeleton is the one memoized, persisted intermediate.</p>
    <div class="chain-scroll"><div class="pills">
      <span class="pill">GalaxyConfig</span><span class="link">→</span>
      <span class="pill">density field</span><span class="link">→</span>
      <span class="pill">galaxy skeleton</span><span class="link">→</span>
      <span class="pill">seeding passes</span><span class="link">→</span>
      <span class="pill">per-hex generation (never persisted)</span>
    </div></div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">the settled board enters the epoch loop</span></div>

  <section class="tier">
    <div class="tier-head"><span class="tier-tag">Epoch loop</span><h2>Seven Phases per Step</h2>
      <span class="nature">frame/simulation-flow.md · one controller touchpoint (Intent) · decisions on belief, consequences on truth</span></div>

    <div class="subgroup" id="phase-pc"><h3>1 · Perception</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-mk"><h3>2 · Markets</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-al"><h3>3 · Allocation</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-in"><h3>4 · Intent</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-rs"><h3>5 · Resolution</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-id"><h3>6 · Interior &amp; Demographics</h3><div class="grid"></div></div>
    <div class="arrow"><span class="stem"></span><span class="head">▼</span></div>
    <div class="subgroup" id="phase-ch"><h3>7 · Chronicle</h3><div class="grid"></div></div>

    <div class="arrow"><span class="stem"></span><span class="head">↺</span><span class="lbl">Chronicle → next step's Perception (news arriving is next step's history)</span></div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">underlying every phase, owned by none of them</span></div>

  <section class="tier">
    <div class="tier-head"><span class="tier-tag">Structural</span><h2>Cross-Cutting &amp; Structural</h2>
      <span class="nature">standing vocabulary, architecture, and discipline the loop runs on — no single phase fires these</span></div>
    <div class="grid" id="flow-crosscut"></div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">the artifact is read by the consuming surfaces</span></div>

  <section class="tier">
    <div class="tier-head"><span class="tier-tag">Surfaces</span><h2>Inspection &amp; Rendering</h2>
      <span class="nature">downstream consumers of the artifact</span></div>
    <div class="grid" id="flow-inspection"></div>
  </section>

</section>
```

- [ ] **Step 3: Structural check (expect pass)**

Run:
```bash
for id in phase-pc phase-mk phase-al phase-in phase-rs phase-id phase-ch flow-crosscut flow-inspection; do \
  printf '%s ' "$id"; grep -c "id=\"$id\"" docs/diagrams/generation-flow.html; done
```
Expected: each id count `1`.

- [ ] **Step 4: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Flow view skeleton — clocks, pipeline, seven phase shells, inspection band"
```

---

## Task 3: Flow phase rosters — Perception, Markets, Allocation

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (fill grids in `#phase-pc`, `#phase-mk`, `#phase-al`)

**Interfaces:**
- Consumes: `#phase-pc .grid`, `#phase-mk .grid`, `#phase-al .grid` from Task 2; the `.node.mech` card structure and chip/sub classes from Task 1; Appendix A rows for the ids listed.
- Produces: 3 + 21 + 5 = 29 `.node.mech` cards.

**Card template (this is the exact structure every mech card uses in both views — copy the `name`, chip class+label, `tag` text, and any `sub` lines from the node's Appendix A row):**

```html
<div class="node mech done">
  <div class="node-head"><span class="name">Order-book market engine</span><span class="chip done">IMPL</span></div>
  <p class="tag">the market IS the set of open buy/sell orders (EVE model); physical escrow (sells hold goods, buys hold credits); reference price is the persistent readout</p>
</div>
```

A card **with** sub-lines (gap chip example, from l1k):

```html
<div class="node mech gap">
  <div class="node-head"><span class="name">Stockpiles &amp; procurement</span><span class="chip gap">GAP</span></div>
  <p class="tag">stock has an address (resting order / larder / laydown yard / in-transit); per-port procurement toward standing targets from reserve treasury; depot tiers bank &amp; slow decay; local siege buffering</p>
  <ul class="subs">
    <li class="gap-item">perishability — design says decay compounds; reserves store loss-free (gap 6)</li>
    <li class="gap-item">procurement contract objects — escrowed contracts stand in as mechanical stockpile-target procurement (gap 3)</li>
  </ul>
</div>
```

Chip labels: `done`→`IMPL`, `spec`→`SPEC`, `gap`→`GAP`. Escape `&` as `&amp;`, `<`/`>` as `&lt;`/`&gt;` (several tags contain `↔`, `×`, `→`, `∈`, `∝`, `∩` — those are safe literal Unicode; only `&`, `<`, `>` need escaping).

- [ ] **Step 1: Structural check (expect fail)**

Run: `grep -c 'class="node mech' docs/diagrams/generation-flow.html`
Expected: `0` (no mech cards yet).

- [ ] **Step 2: Fill `#phase-pc .grid` (ids n2, n3, fl8)**

```html
<div class="node mech done">
  <div class="node-head"><span class="name">Perception state</span><span class="chip done">IMPL</span></div>
  <p class="tag">per-actor compressed beliefs: stance table + belief snapshots per (observer, subject) frozen at last refresh (refresh when elapsed years cover news delay); self-facts fresh; corporate perceives own capability</p>
  <ul class="subs"><li class="spec-item">perceived-price arbitrage — belief carries no partner prices (gap 2)</li></ul>
</div>
<div class="node mech done">
  <div class="node-head"><span class="name">Stances &amp; reputation</span><span class="chip done">IMPL</span></div>
  <p class="tag">news arrival updates stance filtered through observer temperament; reputation derived (never stored) as per-audience stance aggregates, consumed as gates</p>
</div>
<div class="node mech done">
  <div class="node-head"><span class="name">Information carriage</span><span class="chip done">IMPL</span></div>
  <p class="tag">news speed/lane = f(posted traffic frequency); courier/scout fleets are deliberate info assets; player carrying news is this at individual scale</p>
  <ul class="subs"><li class="spec-item">courier fast-paths — news travels traffic only; couriers/scouts/news-carrying player are play-clock (gap 12)</li></ul>
</div>
```

- [ ] **Step 3: Fill `#phase-mk .grid` (ids l0a, l0c, l0d, l0e, l0i, l0j, l0k, l1a, l1b, l1c, l1d, l1e, l1f, l1g, l1h, l1i, l1k, l1l, l1m, co3, fl5)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Commodity vocabulary</span><span class="chip done">IMPL</span></div>
  <p class="tag">17 goods in raw/processed/capital tiers; recipe chains 1–4 nodes deep; standard (exotics-free) vs advanced (exotics-gated) variants, tech-tier-gated</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Demand model</span><span class="chip done">IMPL</span></div>
  <p class="tag">priority bands: population (subsistence/SoL/luxury, embodiment-modulated) · industry · movement (Fuel) · military · technology (Refined Exotics × Compute)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Legality schema</span><span class="chip done">IMPL</span></div>
  <p class="tag">per-polity law code legal/restricted/prohibited + tariff; prohibition → black-market demand at margin</p></div>
<div class="node mech spec"><div class="node-head"><span class="name">Sentient trafficking</span><span class="chip spec">SPEC</span></div>
  <p class="tag">illicit population flow against the gradient toward low-rights polities; crime vs the population substrate</p>
  <ul class="subs"><li class="spec-item">unmodeled in both commodities and migration (gap 5)</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">Production formula</span><span class="chip gap">GAP</span></div>
  <p class="tag">output = base(type,tier) × terrain × labor × machineryGrade × automation(compute)</p>
  <ul class="subs"><li class="gap-item">automation term — production formula accepts it; Markets passes 0.0 (gap 11)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Organic baseline</span><span class="chip done">IMPL</span></div>
  <p class="tag">unserviced settlements subsistence-farm/craft locally; small enough that facilities dominate</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Market geography</span><span class="chip done">IMPL</span></div>
  <p class="tag">one market per port at service-area∩lane-network; per-good price/last-cleared-qty/mean-grade + black book; connectivity is price structure; wilds have no market</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Order-book market engine</span><span class="chip done">IMPL</span></div>
  <p class="tag">the market IS the set of open buy/sell orders (EVE model); physical escrow (sells hold goods, buys hold credits); reference price is the persistent readout</p></div>
<div class="node mech done"><div class="node-head"><span class="name">The market step</span><span class="chip done">IMPL</span></div>
  <p class="tag">fixed 9-step order: expiry sweep → freight sail → requote → supply lands → escrowed demand bids → spread run → matching (price-time priority, MAKER-price fill) → reference drift → clearing consequences</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Per-owner quote decay</span><span class="chip gap">GAP</span></div>
  <p class="tag">(design) sold-out sellers raise, glutted cut</p>
  <ul class="subs"><li class="gap-item">"Deviation, flagged" (markets.md §step 3) — discovery lives in reference drift instead</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">Relay bids</span><span class="chip gap">GAP</span></div>
  <p class="tag">cheap-end sovereign bids at own reference to stage re-export; hop-by-hop diffusion; entrepôts emerge</p>
  <ul class="subs"><li class="gap-item">"Kept past B2, flagged" (markets.md) — hop diffusion stand-in, retires with multi-hop actor runs</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Freight / shipments</span><span class="chip done">IMPL</span></div>
  <p class="tag">a haul = Shipment (origin, dest, cargo, lane route); leg-years priced at departure; blockade/quarantine/dead-gate stalls it; piracy/war-interdiction rolls per sail</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Spread run</span><span class="chip done">IMPL</span></div>
  <p class="tag">posted fleet's owner trades its lane gradient with own capital: lift cheap asks, sail, post at dear end; absorption reads real resting bids above delivered break-even</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Courier contracts</span><span class="chip gap">GAP</span></div>
  <p class="tag">internal logistics as a market: courier posts (origin, dest, escrowed cargo+fee); board clears (priority, id); War priority outbids commerce; requisition channel rides it</p>
  <ul class="subs"><li class="gap-item">ranking deviation (markets.md §courier) — ranks (priority, id), fee prices poster cost only</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Household income &amp; labor share</span><span class="chip done">IMPL</span></div>
  <p class="tag">facilities pay a labor share of revenue to staffing segments (shrinks with automation); SoL derives from real wages at local prices</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Wealth &amp; taxation</span><span class="chip done">IMPL</span></div>
  <p class="tag">transaction tax on sales + tariffs on cross-border freight + state-facility income; true wealth = ledger + asset book (emergent readout)</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Stockpiles &amp; procurement</span><span class="chip gap">GAP</span></div>
  <p class="tag">stock has an address (resting order / larder / laydown yard / in-transit); per-port procurement toward standing targets from reserve treasury; depot tiers bank &amp; slow decay; local siege buffering</p>
  <ul class="subs"><li class="gap-item">perishability — design says decay compounds; reserves store loss-free (gap 6)</li><li class="gap-item">procurement contract objects — escrowed contracts stand in as mechanical stockpile-target procurement (gap 3)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Interdiction strain</span><span class="chip done">IMPL</span></div>
  <p class="tag">per-lane realized-vs-potential trade value minus smuggling leakage; measured where it happens</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Sanctions &amp; tariffs</span><span class="chip done">IMPL</span></div>
  <p class="tag">tariff schedule collected at the entered gate; sanction = non-war lane-legality closure; both evadable at margin, both feed trade→relations hook</p>
  <ul class="subs"><li class="spec-item">sanctions closure — lane-legality closure machinery absent (gap 4)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Portfolio &amp; operations</span><span class="chip done">IMPL</span></div>
  <p class="tag">owns facilities/freighters/depots/routes across borders; speculation is the business (spread runs on own capital); internal logistics on courier contracts; vertical integration</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Fleet object &amp; postures</span><span class="chip done">IMPL</span></div>
  <p class="tag">(id, owner, location, composition, posture, commander, supply); postures Posted/Escort/Patrol/Blockade/Expedition-Convoy/Reserve</p></div>
```

- [ ] **Step 4: Fill `#phase-al .grid` (ids l1j, l1n, l1p, l1q, fl4)**

```html
<div class="node mech gap"><div class="node-head"><span class="name">Credit / loans</span><span class="chip gap">GAP</span></div>
  <p class="tag">loan objects (lender, borrower, principal, rate, term); default → reputation/relations hit, collateral seizure; no banks, lenders are whoever holds surplus</p>
  <ul class="subs"><li class="gap-item">structural debt spiral (HANDOFF/SH); LoanRatePerYear a dead knob; 2×-lender gate kills the credit market epochs 1–4</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Construction / projects</span><span class="chip done">IMPL</span></div>
  <p class="tag">every in-flight work = a project with a rate contract: BuildCost ÷ ConstructionYears per-year basket + wages; bids as a market participant into a laydown yard; advances by scarcest-input fraction; priority-ordered feeds; abandon clock</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Condition &amp; ownership</span><span class="chip done">IMPL</span></div>
  <p class="tag">facilities carry condition (decays w/o upkeep, war-damaged, repaired); ownership transfers by sale/seizure/nationalization/conquest, each a conserved ledger event</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Technology</span><span class="chip done">IMPL</span></div>
  <p class="tag">4 per-polity domains (Industrial/Military/Astrogation/Life); geometric tier ladders unlock ceilings/regions; research consumes Refined Exotics × Compute in Allocation; diffusion via trade/salvage/espionage</p>
  <ul class="subs"><li class="spec-item">espionage diffusion channel reserved by design (gap 13)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Hull production</span><span class="chip done">IMPL</span></div>
  <p class="tag">shipyards convert Ship Components (+Armaments/+Compute) into hulls; hull-batch project anchored at a yard; yard tier caps concurrent batch work</p></div>
```

- [ ] **Step 5: Structural check (expect pass)**

Run: `grep -c 'class="node mech' docs/diagrams/generation-flow.html`
Expected: `29`.

- [ ] **Step 6: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Flow rosters — Perception, Markets, Allocation phases"
```

---

## Task 4: Flow phase rosters — Intent, Resolution, Interior, Chronicle + Cross-Cutting band + Inspection band

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (fill `#phase-in`, `#phase-rs`, `#phase-id`, `#phase-ch`, `#flow-crosscut`, `#flow-inspection`)

**Interfaces:**
- Consumes: the phase grids, `#flow-crosscut`, and `#flow-inspection` from Task 2; the card template from Task 3.
- Produces: 7 + 10 + 19 + 5 + 15 + 4 = 60 more `.node.mech` cards (Flow total → 89).

- [ ] **Step 1: Structural check (expect the pre-task count)**

Run: `grep -c 'class="node mech' docs/diagrams/generation-flow.html`
Expected: `29` (from Task 3; this task adds 60 → 89).

- [ ] **Step 2: Fill `#phase-in .grid` (ids l1o, co2, p11, fl9, i2, i4, i9)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Plan packing</span><span class="chip done">IMPL</span></div>
  <p class="tag">standing plan packed against real capability: income/yr + savings drawdown; colony batches boosted to front when expansion points sit hull-less</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Corporate controller</span><span class="chip gap">GAP</span></div>
  <p class="tag">standing plan (polity planner machinery at corp scope) packed against income + savings drawdown; dividend rate; lobby targets; risk appetite</p>
  <ul class="subs"><li class="gap-item">plan scope — "Scoped, flagged" (corporations.md §controller): plans cover facilities; routes/gates opportunistic, hulls immediate</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Temperament composition</span><span class="chip done">IMPL</span></div>
  <p class="tag">Intent-AI personality = species disposition × official ideology × ruler personality × faction pressure, weighted by government form</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Commanders</span><span class="chip gap">GAP</span></div>
  <p class="tag">fleets above a threshold take a commander role; personality biases posture AI; renown accrues; age/die/succeed/defect</p>
  <ul class="subs"><li class="gap-item">boldness bias missing (gap 9)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Native policy</span><span class="chip done">IMPL</span></div>
  <p class="tag">on covering a pre-spaceflight homeworld: protectorate / integrate / exploit / uplift (Intent act, ideology-weighted, reputation-bearing)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Expansion prices neighbors</span><span class="chip done">IMPL</span></div>
  <p class="tag">colony valuation discounts a site per entangled foreign domain; founding costs instant tension with each; borders contiguous by choice</p></div>
<div class="node mech done"><div class="node-head"><span class="name">War causes</span><span class="chip done">IMPL</span></div>
  <p class="tag">tension discharges through a casus belli menu (economic/ideological/political/spatial/spark); spark rolls in high-friction space; aims scale with hatred → annihilation when saturated</p></div>
```

- [ ] **Step 3: Fill `#phase-rs .grid` (ids l0h, fl6, fl7, i6, i7, i8, i10, i11, i12, i13)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Lane / gate model</span><span class="chip done">IMPL</span></div>
  <p class="tag">a lane = linked gate pair (one per port system); reach = min gate tier; capacity/speed from tiers; anti-web rule; crossing fees by gate owner</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Movement &amp; supply</span><span class="chip done">IMPL</span></div>
  <p class="tag">three leg types (intra-domain/lane-hop/off-lane); off-lane gated on endurance floor; fleets draw fuel/upkeep from nearest owned port; unsupplied lose readiness then hulls</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Attrition &amp; wreckage</span><span class="chip gap">GAP</span></div>
  <p class="tag">losses conserve into wreckage at the death hex → salvage sites &amp; battlefield POIs; piracy risk/lane = lawlessness × cargo value − escort vectors</p>
  <ul class="subs"><li class="gap-item">piracy-risk-into-profit not modeled (gap 10)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Federation</span><span class="chip done">IMPL</span></div>
  <p class="tag">merge gate (sustained alliance + warmth + ideology compat + openness + cohesion); entangled friendly borders push to fusion; fused polity is NEW (weighted composition, fresh form)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Vassalage</span><span class="chip done">IMPL</span></div>
  <p class="tag">asymmetric rung: imposed by settlement or chosen under threat; tribute/defense/policy-lock; exits absorption (drift) &amp; secession (bid)</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Dynastic instruments</span><span class="chip gap">GAP</span></div>
  <p class="tag">marriages/wardships buy warmth &amp; create succession claims (tension pointed the other way); rare personal unions = federation fast-path</p>
  <ul class="subs"><li class="gap-item">personal unions missing (gap 9)</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">War conduct</span><span class="chip gap">GAP</span></div>
  <p class="tag">theater/objective model: assignment per doctrine+commander → per-objective engagement on fleet vectors (fortification, supply, competence, rolls) → sieges (reserves, fortress tier, relief); mobilization is a ramp not a switch</p>
  <ul class="subs"><li class="gap-item">depth — occupation objectives, defensive mirrors, raidable supply objectives missing (gap 9)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Front supply lines</span><span class="chip done">IMPL</span></div>
  <p class="tag">war force draws upkeep from nearest owned port (forward depot); quartermaster stocks it via War-priority couriers; war interdiction rolls seizure per contested sail, escorts damp deterministically; starvation bites readiness</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Allied belligerents</span><span class="chip done">IMPL</span></div>
  <p class="tag">defense-alliance partners join as supporters under war leaders; settlements negotiated between leaders; allied gains/grievances flow through the leader's table</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Termination &amp; settlement</span><span class="chip gap">GAP</span></div>
  <p class="tag">break on political collapse / exhaustion / capital loss / extinction; settlement negotiated from per-objective outcomes (cede/reparations/vassalize/imposed legality/white peace); accept when perceived cost &gt; settlement (stale, so wars overrun)</p>
  <ul class="subs"><li class="gap-item">imposed-legality settlement missing (gap 9)</li></ul></div>
```

- [ ] **Step 4: Fill `#phase-id .grid` (ids co1, co4, co5, co6, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p12, p13, i1, i3, i5)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Corporate founding</span><span class="chip done">IMPL</span></div>
  <p class="tag">simulation watches persistent profit niches (price gradient / unexploited deposit / unserved route) over consecutive epochs; charter event via graduation; niche stamps character</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Corporate influence</span><span class="chip done">IMPL</span></div>
  <p class="tag">lobby spending strengthens aligned factions (dividends → elite faction wealth); sanction evasion by re-flagging through subsidiaries</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Corporate death &amp; estates</span><span class="chip done">IMPL</span></div>
  <p class="tag">bankruptcy (default cascade) / nationalization / niche death; estates-pass settles orders, cargo, jobs, projects, credits, debt conservatively</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Outlaw institutions</span><span class="chip gap">GAP</span></div>
  <p class="tag">same niche rule founds cartels (black-book) &amp; pirate bands (raiding niche = lawlessness × cargo value); based at ruin/haven POIs</p>
  <ul class="subs"><li class="gap-item">piracy-risk-pricing not priced into freight profit (gap 10)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Population segments</span><span class="chip done">IMPL</span></div>
  <p class="tag">(species, culture, size, SoL, ideology distribution); domain-level state (hex is a projection); conserved, identity travels; mixed by conquest/migration/diaspora</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Demographics</span><span class="chip done">IMPL</span></div>
  <p class="tag">growth = f(SoL, provisions, embodiment); machine populations grow by manufacture (Machinery+Compute), age out when cut off; famine/war shrink</p></div>
<div class="node mech gap"><div class="node-head"><span class="name">Culture</span><span class="chip gap">GAP</span></div>
  <p class="tag">registry entities, species-rooted, syllable flavor names systems/ships/characters; spread by migration/conquest</p>
  <ul class="subs"><li class="gap-item">drift — mint-at-schism works; separation-split &amp; slow blending undone (gap 7)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Ideology</span><span class="chip done">IMPL</span></div>
  <p class="tag">4 axes (Authority↔Autonomy · Communal↔Individual · Open↔Insular · Sacral↔Material); segment distributions drift with lived conditions; official = weighted opinion × institutional inertia</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Migration</span><span class="chip done">IMPL</span></div>
  <p class="tag">per-step segment flows along SoL/safety/affinity/opportunity gradients × distance/lane access; refugees (fast) &amp; diasporas (memory-carrying minorities)</p>
  <ul class="subs"><li class="spec-item">trafficking — illicit flow unmodeled (gap 5)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Faction formation</span><span class="chip done">IMPL</span></div>
  <p class="tag">coalesces when a coherent interest diverges from rule; six bases (ideological/cultural/regional/corporate/military/sacral); state = basis, strength, agenda, militancy</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Faction pressure</span><span class="chip done">IMPL</span></div>
  <p class="tag">policies drift toward strong factions (bounded); appeasement spending buys off; unappeased accumulate grievance</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Graduation</span><span class="chip done">IMPL</span></div>
  <p class="tag">strength × grievance &gt; legitimacy × enforcement → Schism (polity) / Coup (ruler, → civil war) / Charter (corp) / Revolt (failed)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Legitimacy &amp; cohesion</span><span class="chip done">IMPL</span></div>
  <p class="tag">legitimacy = f(SoL trend, ideology gap, war outcomes, prestige, cultural accommodation); cohesion = aggregate × structural strain; low cohesion lowers graduation thresholds</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Government forms</span><span class="chip done">IMPL</span></div>
  <p class="tag">closed catalog of 8 (Autocracy/Collective/Assembly/Syndicate/Theocracy/Hive Unity/Machine Consensus/Steward Dynasty) seated in ideology × species; sets succession, inertia, faction tolerance; changes through graduation</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Characters</span><span class="chip done">IMPL</span></div>
  <p class="tag">sparse; generated on demand from (institution, culture, species, seed); personality = ideology position + boldness/zeal/competence/ambition; lifespan/succession; dynasties; notables (hero/founder/prophet/pirate-lord/magnate/explorer); derivable biography</p>
  <ul class="subs"><li class="spec-item">personal acts — 11 unarmed contract acts unbuilt (gap 1)</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">Plagues</span><span class="chip gap">GAP</span></div>
  <p class="tag">outbreak ∝ density × SoL deficit × exposure; propagates the lane graph with traffic; Medicine mitigation; quarantine = self-imposed interdiction; burns out; memorial residue</p>
  <ul class="subs"><li class="gap-item">depth — excavation-release, Medicine mitigation, memorial POIs, era-signature missing (gap 8)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Contact</span><span class="chip done">IMPL</span></div>
  <p class="tag">polities meet when reach overlaps (expansion/trade/news); first contact composes stance from temperament × strangeness × pre-arrived reputation</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Late-emergence resolution</span><span class="chip done">IMPL</span></div>
  <p class="tag">emergence in free space = new polity; inside claimed space resolves by host native policy (vassal/autonomous member/suppressed + liberation casus belli)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Relations state</span><span class="chip done">IMPL</span></div>
  <p class="tag">per-pair warmth (interdependence) + tension (friction, the war-pressure gauge, decays only when sources resolve); treaty rungs (trade pact / non-aggression / defense alliance / federation-vassalage) at mutual consent</p></div>
```

- [ ] **Step 5: Fill `#phase-ch .grid` (ids n1, n4, n5, n6, i14)**

```html
<div class="node mech done"><div class="node-head"><span class="name">News pulses</span><span class="chip done">IMPL</span></div>
  <p class="tag">Chronicle emits pulses for public events above magnitude floors; travel the lane graph at traffic-derived speed, attenuating with distance; couriers/scouts/player are fast paths</p>
  <ul class="subs"><li class="spec-item">fast paths — couriers/scouts/player unbuilt (gap 12)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Event grammar</span><span class="chip done">IMPL</span></div>
  <p class="tag">one schema across four clocks: (id, world-year, clock stratum, type, actors[], location, magnitude, valence, visibility, payload); 8 type families; visibility public/regional/secret; indexes are views</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Chronicle views</span><span class="chip done">IMPL</span></div>
  <p class="tag">queries over the one log at every zoom: galaxy (era detection clusters epochs by signature) / polity (reign arc) / character (biography) / place (hex annotation &amp; archaeology)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">POI compiler</span><span class="chip done">IMPL</span></div>
  <p class="tag">runs inside Chronicle every epoch, converting qualifying events into anchored POIs immediately (battlefields→salvage, ruins→lawlessness, razed capitals→claim anchors, memorials, precursor sites); live sim effects; one-anchor-per-hex by magnitude; decay as consumed</p></div>
<div class="node mech done"><div class="node-head"><span class="name">War aftermath</span><span class="chip done">IMPL</span></div>
  <p class="tag">grudges → standing claims (tomorrow's tension); veterans → military factions; heroes mint; wreckage/razed → POIs; conduct reputation travels the news graph</p></div>
```

- [ ] **Step 6: Fill `#flow-crosscut .grid` (ids l0b, l0f, l0g, l0l, fl1, fl2, fl3, n7, x1, x2, x3, x4, d1, d2, d3)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Grade system</span><span class="chip done">IMPL</span></div>
  <p class="tag">every stock = (quantity, grade∈[0,1]); grade flows terrain→chains; Effective(useCase)=qty×GradeMultiplier; tech tier is the grade ceiling; precursor grade above any ceiling</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Infrastructure catalog</span><span class="chip done">IMPL</span></div>
  <p class="tag">15 types in 5 families (keystone port + extraction/processing/heavy/support); each tier 1–3, build cost, construction time, upkeep, hex anchor; siting rules</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Port &amp; domain model</span><span class="chip done">IMPL</span></div>
  <p class="tag">keystone port: local service radius + gate slots (two growth axes); territory = union of port service areas (derived, never stored); domain overlap = contested zone</p></div>
<div class="node mech spec"><div class="node-head"><span class="name">Retail projection</span><span class="chip spec">SPEC</span></div>
  <p class="tag">play-clock items are retail instances sampling local (good,grade,qty) stocks; tail-sampled exceptional items</p>
  <ul class="subs"><li class="spec-item">play-clock, unbuilt</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">Chassis grid</span><span class="chip gap">GAP</span></div>
  <p class="tag">design = role × size cell (Freight/Escort/Line/Carrier/Scout/Colony/Special); instantiated per polity from embodiment × culture × tech × grade</p>
  <ul class="subs"><li class="gap-item">Carrier role unused (gap 10)</li></ul></div>
<div class="node mech gap"><div class="node-head"><span class="name">Design sheet</span><span class="chip gap">GAP</span></div>
  <p class="tag">two-layer stat model: ~15-stat sheet (Combat/Mobility/Capacity/Operations) + epoch aggregation into vectors; grade/tech act per-stat; refit variants</p>
  <ul class="subs"><li class="gap-item">refit variants unused (gap 10)</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Design lineages</span><span class="chip done">IMPL</span></div>
  <p class="tag">designs drift along named lineages/marks over epochs — fleet composition reads as cultural history</p></div>
<div class="node mech done"><div class="node-head"><span class="name">World-state handoff</span><span class="chip done">IMPL</span></div>
  <p class="tag">final artifact layer: complete registries + deliberately open threads; resumability (same machine at play tick); controller handover; log never closes; delta boundary (save = config + artifact + deltas + log continuation)</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Controller contract</span><span class="chip done">IMPL</span></div>
  <p class="tag">Decide(perceivedState) → (policies, acts) per actor kind (polity/corp/character); the Intent-phase API = the player UI surface</p>
  <ul class="subs"><li class="spec-item">11 unarmed acts (gap 1); armed: found-colony, declare-war, treaty, settlement-response, nationalize, vassalage, dynastic instrument, quarantine</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Price signal</span><span class="chip done">IMPL</span></div>
  <p class="tag">market-price-derived valuations in grade-effective units are the one value language: expansion attractiveness, war-goal value, migration pull, investment, siting all read it</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Event grammar (interface)</span><span class="chip done">IMPL</span></div>
  <p class="tag">every subsystem emits one schema; L4 owns it; emitting well-formed history is a requirement on every mechanic</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Pressure → graduation</span><span class="chip done">IMPL</span></div>
  <p class="tag">L2 faction machinery is the sole factory for new institutions; L3 consumes schisms, L1 consumes charters; emergence schedule is the one non-faction origin</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Determinism discipline</span><span class="chip done">IMPL</span></div>
  <p class="tag">stateless hash rolls keyed (step, actor id, channel 0–73); fixed iteration order; config artifact-stamped; byte-identity at coarse &amp; fine ticks</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Artifact layers</span><span class="chip done">IMPL</span></div>
  <p class="tag">~22 versioned registry layers (ports, lanes, facilities, designs, fleets, wreckage, segments, markets, loans, characters, dynasties, factions, corporations, relations, wars, beliefs, pulses, POIs, plagues…); delta saves = base + changed layers + log continuation; hex tier never persisted</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Four-clock rate model</span><span class="chip done">IMPL</span></div>
  <p class="tag">all rates in world-years (P7); epoch = 25y integration step, not a unit; durations are world-time state; coarse/fine sample the same durations</p></div>
```

- [ ] **Step 7: Fill `#flow-inspection .grid` (ids r1, r2, r3, r4)**

```html
<div class="node mech done"><div class="node-head"><span class="name">Inspector REPL</span><span class="chip done">IMPL</span></div>
  <p class="tag">epoch run/save/load (22-layer), watch, emap layers, panels, threads, estep (fine tick), delta saves, belief/news/era/poi queries</p></div>
<div class="node mech done"><div class="node-head"><span class="name">Unity atlas</span><span class="chip done">IMPL</span></div>
  <p class="tag">rebuilt against the settled model (K1–K5 merged): skeleton → lenses → panels → timeline → system stage; five LOD bands; map/system-stage lenses; typed pickables → panels</p>
  <ul class="subs"><li class="spec-item" style="color:var(--muted)">was PoC — the key staleness fix in this rebuild</li></ul></div>
<div class="node mech done"><div class="node-head"><span class="name">Sim-health harness</span><span class="chip done">IMPL</span></div>
  <p class="tag">MetricRegistry + per-phase MoneyRow; conservation residual ≈1e-8; ensemble sweep runner; ehealth; in-memory only, never serialized</p></div>
<div class="node mech spec"><div class="node-head"><span class="name">Game layer / play clock</span><span class="chip spec">SPEC</span></div>
  <p class="tag">live game: player verbs, perceived-price trading, news-carrying; the acceptance gap list</p>
  <ul class="subs"><li class="spec-item">future</li></ul></div>
```

- [ ] **Step 8: Structural check (expect pass)**

Run: `grep -c 'class="node mech' docs/diagrams/generation-flow.html`
Expected: `89`.

- [ ] **Step 9: Visual verification (Flow view complete)**

Open the file, stay on the Flow tab. Confirm: all seven phase subgroups show cards, connected top-to-bottom by ▼ arrows with the ↺ back-edge label after Chronicle; Markets is the densest phase card grid; the Cross-Cutting & Structural band (15 cards) appears after Chronicle and before Inspection; gap cards show a dashed orange border + `GAP` chip + orange sub-lines, spec cards a gold `SPEC` chip, done cards a green `IMPL` chip — three unmistakably distinct treatments; the Inspection band shows four cards including Unity atlas as `IMPL`. Check no card overflows its column and the page body does not scroll horizontally (only `.chain-scroll` strips may scroll internally).

- [ ] **Step 10: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Flow rosters — Intent/Resolution/Interior/Chronicle + crosscut + inspection bands (89 cards)"
```

---

## Task 5: Blocks view — Genesis, L0 Substrate, L1 Economy

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (fill `<section id="view-blocks">`)

**Interfaces:**
- Consumes: `#view-blocks`, `.tier`, `.tier-head`, `.opr`, `.grid`, `.arrow`, the mech card template + chip/sub classes.
- Produces: the Blocks container's opening groups with stable ids `#blk-genesis`, `#blk-l0`, `#blk-l1` (each a `.tier` with a `.grid`). Tasks 6–7 append sibling `.tier`/strip sections after `#blk-l1`. Adds 7 + 12 + 17 = 36 mech cards (file total → 125, i.e. 89 Flow + 36 Blocks-so-far).

**Reuse:** Genesis, L0, and L1 mech cards use the **same** name/tag/chip/sub content as their Appendix A rows. Where a node also appears **anywhere** in Flow view — either a phase card (`flow ≠ —` in the strict phase sense) or the `#flow-crosscut` band (every L0/L1/Fleet/L4/interfaces/determinism node except Genesis, per the crosscut amendment in §Tally) — the card text here MUST be byte-identical to that existing Flow copy. Concretely for this task: Grade system, Infrastructure catalog, Port & domain model, and Retail projection are NOT Blocks-only — they already exist in `#flow-crosscut` (added in Task 4) and must be reconciled against it, same as any phase-carded node. Only Genesis's 7 nodes are genuinely new/Blocks-only here.

- [ ] **Step 1: Structural check (expect fail)**

Run: `grep -c 'id="blk-l1"' docs/diagrams/generation-flow.html`
Expected: `0`.

- [ ] **Step 2: Fill the Blocks section opening (Genesis + L0 + L1)**

Replace `<section id="view-blocks" class="stack"></section>` with:

```html
<section id="view-blocks" class="stack">

  <section class="tier" id="blk-genesis">
    <div class="tier-head"><span class="tier-tag">Genesis</span><h2>Deep-Time Substrate</h2>
      <span class="nature">genesis/ · cosmic + evolutionary/precursor — feeds every level below</span></div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Cosmic field stack</span><span class="chip done">IMPL</span></div>
        <p class="tag">conserved per-cell fields (Gas, star cohorts, Metals, Remnants); step loop inflow→transport→star-formation→aging→death&amp;enrichment over ~150 deep-time steps</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Cosmic discrete features</span><span class="chip done">IMPL</span></div>
        <p class="tag">seeded/emergent registry: mergers (stellar streams), globulars, nebulae, AGN accretion epochs</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Habitability history</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-cell scalars: metallicity-floor crossing, last sterilization, stability-since — makes emergence causal</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Biosphere field</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-cell LifeViability/BiosphereAge/Richness/SapiencePotential; step loop abiogenesis→aging→catastrophes→spread→sapience-registration</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Emergence schedule</span><span class="chip done">IMPL</span></div>
        <p class="tag">each sapient origin's spaceflight date = abiogenesis + richness-scaled maturation; staggered polity entry with late-emerger contact bonus</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Precursor waves</span><span class="chip done">IMPL</span></div>
        <p class="tag">coarse civ-arc sim (rise/peak/decline) reusing the space model without markets/characters; vigor classes (grand/pocket); cause-typed endings</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Precursor living residue</span><span class="chip done">IMPL</span></div>
        <p class="tag">machine descendants (seed a present machine-species origin), biosphere engineering, sterilization scars, dormant remnants</p></div>
    </div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">provides → present-day cell fields, habitability, hex-tier anchors</span></div>

  <section class="tier" id="blk-l0">
    <div class="tier-head"><span class="tier-tag">L0</span><h2>Substrate</h2>
      <span class="nature">substrate/ · frame/space-and-travel.md</span></div>
    <div class="opr">
      <div><b>Owns</b> commodity vocabulary, infrastructure vocabulary &amp; siting, natural raster, population stores, lane geometry, market geography</div>
      <div><b>Reads</b> deep-genesis outputs</div>
      <div><b>Provides →</b> Potential(cell, good), habitability, demand profiles, buildable catalog</div>
    </div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Commodity vocabulary</span><span class="chip done">IMPL</span></div>
        <p class="tag">17 goods in raw/processed/capital tiers; recipe chains 1–4 nodes deep; standard (exotics-free) vs advanced (exotics-gated) variants, tech-tier-gated</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Grade system</span><span class="chip done">IMPL</span></div>
        <p class="tag">every stock = (quantity, grade∈[0,1]); grade flows terrain→chains; Effective(useCase)=qty×GradeMultiplier; tech tier is the grade ceiling; precursor grade above any ceiling</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Demand model</span><span class="chip done">IMPL</span></div>
        <p class="tag">priority bands: population (subsistence/SoL/luxury, embodiment-modulated) · industry · movement (Fuel) · military · technology (Refined Exotics × Compute)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Legality schema</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-polity law code legal/restricted/prohibited + tariff; prohibition → black-market demand at margin</p></div>
      <div class="node mech spec"><div class="node-head"><span class="name">Sentient trafficking</span><span class="chip spec">SPEC</span></div>
        <p class="tag">illicit population flow against the gradient toward low-rights polities; crime vs the population substrate</p>
        <ul class="subs"><li class="spec-item">unmodeled in both commodities and migration (gap 5)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Infrastructure catalog</span><span class="chip done">IMPL</span></div>
        <p class="tag">15 types in 5 families (keystone port + extraction/processing/heavy/support); each tier 1–3, build cost, construction time, upkeep, hex anchor; siting rules</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Port &amp; domain model</span><span class="chip done">IMPL</span></div>
        <p class="tag">keystone port: local service radius + gate slots (two growth axes); territory = union of port service areas (derived, never stored); domain overlap = contested zone</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Lane / gate model</span><span class="chip done">IMPL</span></div>
        <p class="tag">a lane = linked gate pair (one per port system); reach = min gate tier; capacity/speed from tiers; anti-web rule; crossing fees by gate owner</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Production formula</span><span class="chip gap">GAP</span></div>
        <p class="tag">output = base(type,tier) × terrain × labor × machineryGrade × automation(compute)</p>
        <ul class="subs"><li class="gap-item">automation term — production formula accepts it; Markets passes 0.0 (gap 11)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Organic baseline</span><span class="chip done">IMPL</span></div>
        <p class="tag">unserviced settlements subsistence-farm/craft locally; small enough that facilities dominate</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Market geography</span><span class="chip done">IMPL</span></div>
        <p class="tag">one market per port at service-area∩lane-network; per-good price/last-cleared-qty/mean-grade + black book; connectivity is price structure; wilds have no market</p></div>
      <div class="node mech spec"><div class="node-head"><span class="name">Retail projection</span><span class="chip spec">SPEC</span></div>
        <p class="tag">play-clock items are retail instances sampling local (good,grade,qty) stocks; tail-sampled exceptional items</p>
        <ul class="subs"><li class="spec-item">play-clock, unbuilt</li></ul></div>
    </div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">provides → Potential(cell, good), demand profiles, buildable catalog</span></div>

  <section class="tier" id="blk-l1">
    <div class="tier-head"><span class="tier-tag">L1</span><h2>Economy</h2>
      <span class="nature">economy/ · markets, wealth, corporations, infrastructure registries</span></div>
    <div class="opr">
      <div><b>Owns</b> markets, trade flows, wealth ledgers, corporate registry, infrastructure registries, tariffs/sanctions</div>
      <div><b>Reads</b> L0 potentials/demand; L3 constraints; the fleet model; Intent policies</div>
      <div><b>Provides →</b> prices, tax income, corporate revenue, port domains &amp; lane network, throughput, freight capacity, interdiction strain</div>
    </div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Order-book market engine</span><span class="chip done">IMPL</span></div>
        <p class="tag">the market IS the set of open buy/sell orders (EVE model); physical escrow (sells hold goods, buys hold credits); reference price is the persistent readout</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">The market step</span><span class="chip done">IMPL</span></div>
        <p class="tag">fixed 9-step order: expiry sweep → freight sail → requote → supply lands → escrowed demand bids → spread run → matching (price-time priority, MAKER-price fill) → reference drift → clearing consequences</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Per-owner quote decay</span><span class="chip gap">GAP</span></div>
        <p class="tag">(design) sold-out sellers raise, glutted cut</p>
        <ul class="subs"><li class="gap-item">"Deviation, flagged" (markets.md §step 3) — discovery lives in reference drift instead</li></ul></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Relay bids</span><span class="chip gap">GAP</span></div>
        <p class="tag">cheap-end sovereign bids at own reference to stage re-export; hop-by-hop diffusion; entrepôts emerge</p>
        <ul class="subs"><li class="gap-item">"Kept past B2, flagged" (markets.md) — hop diffusion stand-in, retires with multi-hop actor runs</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Freight / shipments</span><span class="chip done">IMPL</span></div>
        <p class="tag">a haul = Shipment (origin, dest, cargo, lane route); leg-years priced at departure; blockade/quarantine/dead-gate stalls it; piracy/war-interdiction rolls per sail</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Spread run</span><span class="chip done">IMPL</span></div>
        <p class="tag">posted fleet's owner trades its lane gradient with own capital: lift cheap asks, sail, post at dear end; absorption reads real resting bids above delivered break-even</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Courier contracts</span><span class="chip gap">GAP</span></div>
        <p class="tag">internal logistics as a market: courier posts (origin, dest, escrowed cargo+fee); board clears (priority, id); War priority outbids commerce; requisition channel rides it</p>
        <ul class="subs"><li class="gap-item">ranking deviation (markets.md §courier) — ranks (priority, id), fee prices poster cost only</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Household income &amp; labor share</span><span class="chip done">IMPL</span></div>
        <p class="tag">facilities pay a labor share of revenue to staffing segments (shrinks with automation); SoL derives from real wages at local prices</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Wealth &amp; taxation</span><span class="chip done">IMPL</span></div>
        <p class="tag">transaction tax on sales + tariffs on cross-border freight + state-facility income; true wealth = ledger + asset book (emergent readout)</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Credit / loans</span><span class="chip gap">GAP</span></div>
        <p class="tag">loan objects (lender, borrower, principal, rate, term); default → reputation/relations hit, collateral seizure; no banks, lenders are whoever holds surplus</p>
        <ul class="subs"><li class="gap-item">structural debt spiral (HANDOFF/SH); LoanRatePerYear a dead knob; 2×-lender gate kills the credit market epochs 1–4</li></ul></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Stockpiles &amp; procurement</span><span class="chip gap">GAP</span></div>
        <p class="tag">stock has an address (resting order / larder / laydown yard / in-transit); per-port procurement toward standing targets from reserve treasury; depot tiers bank &amp; slow decay; local siege buffering</p>
        <ul class="subs"><li class="gap-item">perishability — design says decay compounds; reserves store loss-free (gap 6)</li><li class="gap-item">procurement contract objects — escrowed contracts stand in as mechanical stockpile-target procurement (gap 3)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Interdiction strain</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-lane realized-vs-potential trade value minus smuggling leakage; measured where it happens</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Sanctions &amp; tariffs</span><span class="chip done">IMPL</span></div>
        <p class="tag">tariff schedule collected at the entered gate; sanction = non-war lane-legality closure; both evadable at margin, both feed trade→relations hook</p>
        <ul class="subs"><li class="spec-item">sanctions closure — lane-legality closure machinery absent (gap 4)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Construction / projects</span><span class="chip done">IMPL</span></div>
        <p class="tag">every in-flight work = a project with a rate contract: BuildCost ÷ ConstructionYears per-year basket + wages; bids as a market participant into a laydown yard; advances by scarcest-input fraction; priority-ordered feeds; abandon clock</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Plan packing</span><span class="chip done">IMPL</span></div>
        <p class="tag">standing plan packed against real capability: income/yr + savings drawdown; colony batches boosted to front when expansion points sit hull-less</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Condition &amp; ownership</span><span class="chip done">IMPL</span></div>
        <p class="tag">facilities carry condition (decays w/o upkeep, war-damaged, repaired); ownership transfers by sale/seizure/nationalization/conquest, each a conserved ledger event</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Technology</span><span class="chip done">IMPL</span></div>
        <p class="tag">4 per-polity domains (Industrial/Military/Astrogation/Life); geometric tier ladders unlock ceilings/regions; research consumes Refined Exotics × Compute in Allocation; diffusion via trade/salvage/espionage</p>
        <ul class="subs"><li class="spec-item">espionage diffusion channel reserved by design (gap 13)</li></ul></div>
    </div>
  </section>

</section>
```

- [ ] **Step 3: Structural check (expect pass)**

Run:
```bash
grep -c 'class="node mech' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-genesis"' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-l0"' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-l1"' docs/diagrams/generation-flow.html
```
Expected: `125`, `1`, `1`, `1`.

- [ ] **Step 4: Visual verification**

Open the file, click "Blocks · architecture". Confirm: Genesis, L0, L1 render as stacked tier bands with Owns/Reads/Provides rows on L0 and L1; connector labels ("provides → …") between them; L1 is the densest grid; chips read the same three treatments as Flow. Toggle back to Flow and confirm it still renders (Blocks content did not leak into Flow).

- [ ] **Step 5: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Blocks view — Genesis, L0 Substrate, L1 Economy (125 cards)"
```

---

## Task 6: Blocks view — Corporations, Fleet cross-cutting, L2 Polity

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (append after `#blk-l1`, inside `#view-blocks`)

**Interfaces:**
- Consumes: `#view-blocks`, the tier/opr/grid structure, the mech card template.
- Produces: `#blk-corp`, `#blk-fleet`, `#blk-l2` tiers. Adds 6 + 9 + 13 = 28 mech cards (file total → 153, i.e. 89 Flow + 64 Blocks-so-far). Corp/Fleet/L2 nodes with `flow ≠ —` must be byte-identical to their Flow copies (copy from the Flow section).

- [ ] **Step 1: Structural check (expect fail)**

Run: `grep -c 'id="blk-l2"' docs/diagrams/generation-flow.html`
Expected: `0`.

- [ ] **Step 2: Insert the three tiers immediately before the closing `</section>` of `#view-blocks`** (i.e. after the `#blk-l1` `</section>`)

```html
  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">emergent, non-territorial actors inside L1</span></div>

  <section class="tier" id="blk-corp">
    <div class="tier-head"><span class="tier-tag">L1 · corps</span><h2>Corporations &amp; Outlaws</h2>
      <span class="nature">economy/corporations.md · niche → charter → portfolio</span></div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Corporate founding</span><span class="chip done">IMPL</span></div>
        <p class="tag">simulation watches persistent profit niches (price gradient / unexploited deposit / unserved route) over consecutive epochs; charter event via graduation; niche stamps character</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Corporate controller</span><span class="chip gap">GAP</span></div>
        <p class="tag">standing plan (polity planner machinery at corp scope) packed against income + savings drawdown; dividend rate; lobby targets; risk appetite</p>
        <ul class="subs"><li class="gap-item">plan scope — "Scoped, flagged" (corporations.md §controller): plans cover facilities; routes/gates opportunistic, hulls immediate</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Portfolio &amp; operations</span><span class="chip done">IMPL</span></div>
        <p class="tag">owns facilities/freighters/depots/routes across borders; speculation is the business (spread runs on own capital); internal logistics on courier contracts; vertical integration</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Corporate influence</span><span class="chip done">IMPL</span></div>
        <p class="tag">lobby spending strengthens aligned factions (dividends → elite faction wealth); sanction evasion by re-flagging through subsidiaries</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Corporate death &amp; estates</span><span class="chip done">IMPL</span></div>
        <p class="tag">bankruptcy (default cascade) / nationalization / niche death; estates-pass settles orders, cargo, jobs, projects, credits, debt conservatively</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Outlaw institutions</span><span class="chip gap">GAP</span></div>
        <p class="tag">same niche rule founds cartels (black-book) &amp; pirate bands (raiding niche = lawlessness × cargo value); based at ruin/haven POIs</p>
        <ul class="subs"><li class="gap-item">piracy-risk-pricing not priced into freight profit (gap 10)</li></ul></div>
    </div>
  </section>

  <section class="tier" id="blk-fleet">
    <div class="tier-head"><span class="tier-tag">Cross-cutting</span><h2>Fleet Model</h2>
      <span class="nature">fleets/ships-and-fleets.md · consumed by L1 (freight, piracy) &amp; L3 (combat); owned by neither</span></div>
    <div class="grid">
      <div class="node mech gap"><div class="node-head"><span class="name">Chassis grid</span><span class="chip gap">GAP</span></div>
        <p class="tag">design = role × size cell (Freight/Escort/Line/Carrier/Scout/Colony/Special); instantiated per polity from embodiment × culture × tech × grade</p>
        <ul class="subs"><li class="gap-item">Carrier role unused (gap 10)</li></ul></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Design sheet</span><span class="chip gap">GAP</span></div>
        <p class="tag">two-layer stat model: ~15-stat sheet (Combat/Mobility/Capacity/Operations) + epoch aggregation into vectors; grade/tech act per-stat; refit variants</p>
        <ul class="subs"><li class="gap-item">refit variants unused (gap 10)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Design lineages</span><span class="chip done">IMPL</span></div>
        <p class="tag">designs drift along named lineages/marks over epochs — fleet composition reads as cultural history</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Hull production</span><span class="chip done">IMPL</span></div>
        <p class="tag">shipyards convert Ship Components (+Armaments/+Compute) into hulls; hull-batch project anchored at a yard; yard tier caps concurrent batch work</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Fleet object &amp; postures</span><span class="chip done">IMPL</span></div>
        <p class="tag">(id, owner, location, composition, posture, commander, supply); postures Posted/Escort/Patrol/Blockade/Expedition-Convoy/Reserve</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Movement &amp; supply</span><span class="chip done">IMPL</span></div>
        <p class="tag">three leg types (intra-domain/lane-hop/off-lane); off-lane gated on endurance floor; fleets draw fuel/upkeep from nearest owned port; unsupplied lose readiness then hulls</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Attrition &amp; wreckage</span><span class="chip gap">GAP</span></div>
        <p class="tag">losses conserve into wreckage at the death hex → salvage sites &amp; battlefield POIs; piracy risk/lane = lawlessness × cargo value − escort vectors</p>
        <ul class="subs"><li class="gap-item">piracy-risk-into-profit not modeled (gap 10)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Information carriage</span><span class="chip done">IMPL</span></div>
        <p class="tag">news speed/lane = f(posted traffic frequency); courier/scout fleets are deliberate info assets; player carrying news is this at individual scale</p>
        <ul class="subs"><li class="spec-item">courier fast-paths — news travels traffic only; couriers/scouts/news-carrying player are play-clock (gap 12)</li></ul></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Commanders</span><span class="chip gap">GAP</span></div>
        <p class="tag">fleets above a threshold take a commander role; personality biases posture AI; renown accrues; age/die/succeed/defect</p>
        <ul class="subs"><li class="gap-item">boldness bias missing (gap 9)</li></ul></div>
    </div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">provides → temperament composition, graduations, leadership personality</span></div>

  <section class="tier" id="blk-l2">
    <div class="tier-head"><span class="tier-tag">L2</span><h2>Polity Interior</h2>
      <span class="nature">polity/ · factions, ideology, characters, demographics</span></div>
    <div class="opr">
      <div><b>Owns</b> factions, ideology, government form, characters/roles, succession, cohesion, demographics</div>
      <div><b>Reads</b> L1 (SoL, faction wealth), L3 (war outcomes), L4 (news → opinion)</div>
      <div><b>Provides →</b> temperament composition, stability/schism risk, graduations, leadership personality</div>
    </div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Population segments</span><span class="chip done">IMPL</span></div>
        <p class="tag">(species, culture, size, SoL, ideology distribution); domain-level state (hex is a projection); conserved, identity travels; mixed by conquest/migration/diaspora</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Demographics</span><span class="chip done">IMPL</span></div>
        <p class="tag">growth = f(SoL, provisions, embodiment); machine populations grow by manufacture (Machinery+Compute), age out when cut off; famine/war shrink</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Culture</span><span class="chip gap">GAP</span></div>
        <p class="tag">registry entities, species-rooted, syllable flavor names systems/ships/characters; spread by migration/conquest</p>
        <ul class="subs"><li class="gap-item">drift — mint-at-schism works; separation-split &amp; slow blending undone (gap 7)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Ideology</span><span class="chip done">IMPL</span></div>
        <p class="tag">4 axes (Authority↔Autonomy · Communal↔Individual · Open↔Insular · Sacral↔Material); segment distributions drift with lived conditions; official = weighted opinion × institutional inertia</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Migration</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-step segment flows along SoL/safety/affinity/opportunity gradients × distance/lane access; refugees (fast) &amp; diasporas (memory-carrying minorities)</p>
        <ul class="subs"><li class="spec-item">trafficking — illicit flow unmodeled (gap 5)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Faction formation</span><span class="chip done">IMPL</span></div>
        <p class="tag">coalesces when a coherent interest diverges from rule; six bases (ideological/cultural/regional/corporate/military/sacral); state = basis, strength, agenda, militancy</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Faction pressure</span><span class="chip done">IMPL</span></div>
        <p class="tag">policies drift toward strong factions (bounded); appeasement spending buys off; unappeased accumulate grievance</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Graduation</span><span class="chip done">IMPL</span></div>
        <p class="tag">strength × grievance &gt; legitimacy × enforcement → Schism (polity) / Coup (ruler, → civil war) / Charter (corp) / Revolt (failed)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Legitimacy &amp; cohesion</span><span class="chip done">IMPL</span></div>
        <p class="tag">legitimacy = f(SoL trend, ideology gap, war outcomes, prestige, cultural accommodation); cohesion = aggregate × structural strain; low cohesion lowers graduation thresholds</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Government forms</span><span class="chip done">IMPL</span></div>
        <p class="tag">closed catalog of 8 (Autocracy/Collective/Assembly/Syndicate/Theocracy/Hive Unity/Machine Consensus/Steward Dynasty) seated in ideology × species; sets succession, inertia, faction tolerance; changes through graduation</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Temperament composition</span><span class="chip done">IMPL</span></div>
        <p class="tag">Intent-AI personality = species disposition × official ideology × ruler personality × faction pressure, weighted by government form</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Characters</span><span class="chip done">IMPL</span></div>
        <p class="tag">sparse; generated on demand from (institution, culture, species, seed); personality = ideology position + boldness/zeal/competence/ambition; lifespan/succession; dynasties; notables (hero/founder/prophet/pirate-lord/magnate/explorer); derivable biography</p>
        <ul class="subs"><li class="spec-item">personal acts — 11 unarmed contract acts unbuilt (gap 1)</li></ul></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Plagues</span><span class="chip gap">GAP</span></div>
        <p class="tag">outbreak ∝ density × SoL deficit × exposure; propagates the lane graph with traffic; Medicine mitigation; quarantine = self-imposed interdiction; burns out; memorial residue</p>
        <ul class="subs"><li class="gap-item">depth — excavation-release, Medicine mitigation, memorial POIs, era-signature missing (gap 8)</li></ul></div>
    </div>
  </section>
```

- [ ] **Step 3: Structural check (expect pass)**

Run:
```bash
grep -c 'class="node mech' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-corp"' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-fleet"' docs/diagrams/generation-flow.html; \
grep -c 'id="blk-l2"' docs/diagrams/generation-flow.html
```
Expected: `153`, `1`, `1`, `1`.

- [ ] **Step 4: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Blocks view — Corporations, Fleet, L2 Polity (153 cards)"
```

---

## Task 7: Blocks view — L3, L4, interfaces strip, determinism, Actor taxonomy

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (append after `#blk-l2`, inside `#view-blocks`)

**Interfaces:**
- Consumes: `#view-blocks`, tier/opr/grid structure, card template.
- Produces: `#blk-l3`, `#blk-l4`, `#blk-actors`, `#blk-interfaces`, `#blk-determinism`. Adds 14 (L3) + 7 (L4) + 4 (interfaces) + 3 (determinism) = 28 mech cards, plus one **non-mech** Actor-taxonomy tier (not counted in the 181). File mech total → 181.

**Actor taxonomy note:** spec §2 Blocks lists the Actor taxonomy (`frame/actors.md`) as a cross-cutting card. `actors.md` names five actor kinds; these are not §4 inventory nodes, so they render as a single descriptive tier (no `.mech` cards, so the count stays at 181). Content is drawn from `frame/actors.md`'s kinds: Polity, Corporation, Character, Population, Faction, plus Assets (infrastructure + fleets) as the acted-through layer.

- [ ] **Step 1: Structural check (expect fail)**

Run: `grep -c 'id="blk-determinism"' docs/diagrams/generation-flow.html`
Expected: `0`.

- [ ] **Step 2: Insert the sections immediately before the closing `</section>` of `#view-blocks`**

```html
  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">provides → constraint surfaces (blockades/borders/sanctions), war outcomes, contact events</span></div>

  <section class="tier" id="blk-l3">
    <div class="tier-head"><span class="tier-tag">L3</span><h2>Inter-polity</h2>
      <span class="nature">interpolity/ · relations, wars, treaties, fronts</span></div>
    <div class="opr">
      <div><b>Owns</b> relations matrix, wars, treaties/federations/vassalage, military postures, fronts, battles</div>
      <div><b>Reads</b> L4 stances, L1 prices, L2 composition, the fleet model</div>
      <div><b>Provides →</b> constraint surfaces for L1 (blockades/borders/sanctions), war outcomes for L2, contact events</div>
    </div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Contact</span><span class="chip done">IMPL</span></div>
        <p class="tag">polities meet when reach overlaps (expansion/trade/news); first contact composes stance from temperament × strangeness × pre-arrived reputation</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Native policy</span><span class="chip done">IMPL</span></div>
        <p class="tag">on covering a pre-spaceflight homeworld: protectorate / integrate / exploit / uplift (Intent act, ideology-weighted, reputation-bearing)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Late-emergence resolution</span><span class="chip done">IMPL</span></div>
        <p class="tag">emergence in free space = new polity; inside claimed space resolves by host native policy (vassal/autonomous member/suppressed + liberation casus belli)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Expansion prices neighbors</span><span class="chip done">IMPL</span></div>
        <p class="tag">colony valuation discounts a site per entangled foreign domain; founding costs instant tension with each; borders contiguous by choice</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Relations state</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-pair warmth (interdependence) + tension (friction, the war-pressure gauge, decays only when sources resolve); treaty rungs (trade pact / non-aggression / defense alliance / federation-vassalage) at mutual consent</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Federation</span><span class="chip done">IMPL</span></div>
        <p class="tag">merge gate (sustained alliance + warmth + ideology compat + openness + cohesion); entangled friendly borders push to fusion; fused polity is NEW (weighted composition, fresh form)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Vassalage</span><span class="chip done">IMPL</span></div>
        <p class="tag">asymmetric rung: imposed by settlement or chosen under threat; tribute/defense/policy-lock; exits absorption (drift) &amp; secession (bid)</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Dynastic instruments</span><span class="chip gap">GAP</span></div>
        <p class="tag">marriages/wardships buy warmth &amp; create succession claims (tension pointed the other way); rare personal unions = federation fast-path</p>
        <ul class="subs"><li class="gap-item">personal unions missing (gap 9)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">War causes</span><span class="chip done">IMPL</span></div>
        <p class="tag">tension discharges through a casus belli menu (economic/ideological/political/spatial/spark); spark rolls in high-friction space; aims scale with hatred → annihilation when saturated</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">War conduct</span><span class="chip gap">GAP</span></div>
        <p class="tag">theater/objective model: assignment per doctrine+commander → per-objective engagement on fleet vectors (fortification, supply, competence, rolls) → sieges (reserves, fortress tier, relief); mobilization is a ramp not a switch</p>
        <ul class="subs"><li class="gap-item">depth — occupation objectives, defensive mirrors, raidable supply objectives missing (gap 9)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Front supply lines</span><span class="chip done">IMPL</span></div>
        <p class="tag">war force draws upkeep from nearest owned port (forward depot); quartermaster stocks it via War-priority couriers; war interdiction rolls seizure per contested sail, escorts damp deterministically; starvation bites readiness</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Allied belligerents</span><span class="chip done">IMPL</span></div>
        <p class="tag">defense-alliance partners join as supporters under war leaders; settlements negotiated between leaders; allied gains/grievances flow through the leader's table</p></div>
      <div class="node mech gap"><div class="node-head"><span class="name">Termination &amp; settlement</span><span class="chip gap">GAP</span></div>
        <p class="tag">break on political collapse / exhaustion / capital loss / extinction; settlement negotiated from per-objective outcomes (cede/reparations/vassalize/imposed legality/white peace); accept when perceived cost &gt; settlement (stale, so wars overrun)</p>
        <ul class="subs"><li class="gap-item">imposed-legality settlement missing (gap 9)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">War aftermath</span><span class="chip done">IMPL</span></div>
        <p class="tag">grudges → standing claims (tomorrow's tension); veterans → military factions; heroes mint; wreckage/razed → POIs; conduct reputation travels the news graph</p></div>
    </div>
  </section>

  <div class="arrow"><span class="stem"></span><span class="head">▼</span><span class="lbl">provides → perceived state, chronicle queries, POIs to the hex tier, the handoff</span></div>

  <section class="tier" id="blk-l4">
    <div class="tier-head"><span class="tier-tag">L4</span><h2>Narrative</h2>
      <span class="nature">narrative/ · event log, news, perception, chronicle, POIs, handoff</span></div>
    <div class="opr">
      <div><b>Owns</b> event log, news pulses, per-actor perception, reputation, chronicle/era views, POI compiler, world-state handoff</div>
      <div><b>Reads</b> everything, via events only</div>
      <div><b>Provides →</b> perceived state, chronicle queries, POIs to the hex tier, the handoff</div>
    </div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">News pulses</span><span class="chip done">IMPL</span></div>
        <p class="tag">Chronicle emits pulses for public events above magnitude floors; travel the lane graph at traffic-derived speed, attenuating with distance; couriers/scouts/player are fast paths</p>
        <ul class="subs"><li class="spec-item">fast paths — couriers/scouts/player unbuilt (gap 12)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Perception state</span><span class="chip done">IMPL</span></div>
        <p class="tag">per-actor compressed beliefs: stance table + belief snapshots per (observer, subject) frozen at last refresh (refresh when elapsed years cover news delay); self-facts fresh; corporate perceives own capability</p>
        <ul class="subs"><li class="spec-item">perceived-price arbitrage — belief carries no partner prices (gap 2)</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Stances &amp; reputation</span><span class="chip done">IMPL</span></div>
        <p class="tag">news arrival updates stance filtered through observer temperament; reputation derived (never stored) as per-audience stance aggregates, consumed as gates</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Event grammar</span><span class="chip done">IMPL</span></div>
        <p class="tag">one schema across four clocks: (id, world-year, clock stratum, type, actors[], location, magnitude, valence, visibility, payload); 8 type families; visibility public/regional/secret; indexes are views</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Chronicle views</span><span class="chip done">IMPL</span></div>
        <p class="tag">queries over the one log at every zoom: galaxy (era detection clusters epochs by signature) / polity (reign arc) / character (biography) / place (hex annotation &amp; archaeology)</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">POI compiler</span><span class="chip done">IMPL</span></div>
        <p class="tag">runs inside Chronicle every epoch, converting qualifying events into anchored POIs immediately (battlefields→salvage, ruins→lawlessness, razed capitals→claim anchors, memorials, precursor sites); live sim effects; one-anchor-per-hex by magnitude; decay as consumed</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">World-state handoff</span><span class="chip done">IMPL</span></div>
        <p class="tag">final artifact layer: complete registries + deliberately open threads; resumability (same machine at play tick); controller handover; log never closes; delta boundary (save = config + artifact + deltas + log continuation)</p></div>
    </div>
  </section>

  <section class="tier" id="blk-actors">
    <div class="tier-head"><span class="tier-tag">Cross-cutting</span><h2>Actor Taxonomy</h2>
      <span class="nature">frame/actors.md · who decides, who responds, what is acted through</span></div>
    <p class="desc"><b>Polity</b> (territorial sovereign) · <b>Corporation</b> (emergent, non-territorial) · <b>Character</b> (sparse role-holders + notables) decide through the controller contract. <b>Population</b> (species/culture segments) responds — the demand side and legitimacy base. <b>Faction</b> is the semi-actor pressure bloc, the one factory for new institutions via graduation. <b>Assets</b> — infrastructure (ports, mines, shipyards, fortresses) and fleets — are acted through, concrete and conserved.</p>
  </section>

  <section class="tier" id="blk-interfaces">
    <div class="tier-head"><span class="tier-tag">Interfaces</span><h2>Frame Cross-cutting Contracts</h2>
      <span class="nature">frame/system-map.md §cross-cutting · the four shared interfaces</span></div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Controller contract</span><span class="chip done">IMPL</span></div>
        <p class="tag">Decide(perceivedState) → (policies, acts) per actor kind (polity/corp/character); the Intent-phase API = the player UI surface; enumerated in frame/controller-contract.md</p>
        <ul class="subs"><li class="spec-item">11 unarmed acts (gap 1); armed: found-colony, declare-war, treaty, settlement-response, nationalize, vassalage, dynastic instrument, quarantine</li></ul></div>
      <div class="node mech done"><div class="node-head"><span class="name">Price signal</span><span class="chip done">IMPL</span></div>
        <p class="tag">market-price-derived valuations in grade-effective units are the one value language: expansion attractiveness, war-goal value, migration pull, investment, siting all read it</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Event grammar (interface)</span><span class="chip done">IMPL</span></div>
        <p class="tag">every subsystem emits one schema; L4 owns it; emitting well-formed history is a requirement on every mechanic</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Pressure → graduation</span><span class="chip done">IMPL</span></div>
        <p class="tag">L2 faction machinery is the sole factory for new institutions; L3 consumes schisms, L1 consumes charters; emergence schedule is the one non-faction origin</p></div>
    </div>
  </section>

  <section class="tier" id="blk-determinism">
    <div class="tier-head"><span class="tier-tag">Discipline</span><h2>Determinism &amp; Artifact</h2>
      <span class="nature">frame · P6 · governs every phase</span></div>
    <div class="grid">
      <div class="node mech done"><div class="node-head"><span class="name">Determinism discipline</span><span class="chip done">IMPL</span></div>
        <p class="tag">stateless hash rolls keyed (step, actor id, channel 0–73); fixed iteration order; config artifact-stamped; byte-identity at coarse &amp; fine ticks</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Artifact layers</span><span class="chip done">IMPL</span></div>
        <p class="tag">~22 versioned registry layers (ports, lanes, facilities, designs, fleets, wreckage, segments, markets, loans, characters, dynasties, factions, corporations, relations, wars, beliefs, pulses, POIs, plagues…); delta saves = base + changed layers + log continuation; hex tier never persisted</p></div>
      <div class="node mech done"><div class="node-head"><span class="name">Four-clock rate model</span><span class="chip done">IMPL</span></div>
        <p class="tag">all rates in world-years (P7); epoch = 25y integration step, not a unit; durations are world-time state; coarse/fine sample the same durations</p></div>
    </div>
  </section>
```

- [ ] **Step 3: Structural check (expect pass)**

Run:
```bash
grep -c 'class="node mech' docs/diagrams/generation-flow.html; \
for id in blk-l3 blk-l4 blk-actors blk-interfaces blk-determinism; do \
  printf '%s ' "$id"; grep -c "id=\"$id\"" docs/diagrams/generation-flow.html; done
```
Expected: `181`, then each id `1`.

- [ ] **Step 4: Visual verification (Blocks view complete)**

Open the file on the Blocks tab. Confirm: the full stack Genesis → L0 → L1 → Corps → Fleet → L2 → L3 → L4 → Actors → Interfaces → Determinism renders, with `provides →` connector labels between the levels; Owns/Reads/Provides rows on L0–L4; the Actor-taxonomy tier is prose (no cards); three distinct chip treatments throughout. Confirm no horizontal body scroll.

- [ ] **Step 5: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "feat(diagram): Blocks view — L3, L4, actors, interfaces, determinism (181 cards)"
```

---

## Task 8: Integration — consistency, count, stamp, stale-content scan, full visual pass

**Files:**
- Modify: `docs/diagrams/generation-flow.html` (stamp only; the rest is verification)

**Interfaces:**
- Consumes: the whole file. No new structure produced.

- [ ] **Step 1: Total node count (spec §6 completeness — 96 distinct / 181 rendered)**

Run: `grep -c 'class="node mech' docs/diagrams/generation-flow.html`
Expected: `181` (89 Flow + 92 Blocks; see Appendix A tally). If this differs, a card was dropped or duplicated — reconcile against Appendix A before continuing.

- [ ] **Step 2: Chip distribution check**

Run:
```bash
grep -o 'class="chip [a-z]*"' docs/diagrams/generation-flow.html | sort | uniq -c
```
Expected (chips only; note the four clock chips and Play clock also use `.chip`): `chip done`, `chip spec`, `chip gap` all present. Every node is rendered once per view it appears in: Genesis (7) and Inspection (4) are single-view (11 chips); every other node — including all 15 crosscut-band nodes, now both-view — renders twice (2 × 85 = 170 chips). Total chip count = 11 + 170 = 181, matching Step 1. `gap` nodes (17 distinct: l0i, l1c, l1d, l1g, l1j, l1k, co2, co6, fl1, fl2, fl7, fl9, p3, p13, i8, i10, i13) are all both-view → 34 `gap` chips. `spec` nodes (3 distinct: l0e, l0l, r4) — l0e and l0l are both-view (4 chips), r4 is Flow-only (1 chip) → 5 `spec` chips. The rest are `done`. Exact counts are secondary — the assertion is that all three classes appear and the totals reconcile to 181.

- [ ] **Step 3: Byte-identity spot-check (spec §6 review check — tag/status identical across views)**

For a sample of both-view nodes, confirm the tag string appears exactly twice (once per view) — proving the Flow and Blocks copies are identical:
```bash
for s in "the market IS the set of open buy" "Intent-AI personality = species disposition" \
         "theater/objective model: assignment per doctrine" "per-port procurement toward standing targets" \
         "every stock = (quantity, grade" "stateless hash rolls keyed"; do \
  printf '%s => ' "$s"; grep -c "$s" docs/diagrams/generation-flow.html; done
```
Expected: each `=> 2`. A `1` means the two views diverged on that node's tag — fix the divergent copy so both match Appendix A. (Only Genesis and Inspection nodes are legitimately single-view and should be excluded from this check; every crosscut-band node is now both-view and belongs in it.)

- [ ] **Step 4: Stale-content scan (spec §5 retired items must be absent)**

Run:
```bash
grep -ci 'prototype' docs/diagrams/generation-flow.html; \
grep -c 'PoC' docs/diagrams/generation-flow.html; \
grep -ci 'design passes' docs/diagrams/generation-flow.html; \
grep -c 'class="chip fut"' docs/diagrams/generation-flow.html; \
grep -c 'class="node.*proto"' docs/diagrams/generation-flow.html; \
grep -c 'TBD' docs/diagrams/generation-flow.html; \
grep -c 'TODO' docs/diagrams/generation-flow.html
```
Expected: `prototype` = 0; `PoC` = 1 (only the allowed "was PoC — the key staleness fix" note on the Unity atlas card; if it is 0 that is also fine, and if >1 investigate); `design passes` = 0; `chip fut` = 0; `node proto` = 0; `TBD` = 0; `TODO` = 0. Any nonzero on the retired classes/labels means old markup survived — remove it.

- [ ] **Step 5: Update the header stamp with the live commit**

Resolve the current commit and patch the placeholder:
```bash
COMMIT=$(git rev-parse --short HEAD)
```
Then edit the stamp `<span>` in the header, replacing the literal `COMMIT` token:

Find:
```html
      <span class="stamp">2026-07-12 · COMMIT · A–K roadmap closed; epoch sim implemented; 13 filed gaps + flagged deviations carried</span>
```
Replace `COMMIT` with the resolved short hash (e.g. `2926928`), leaving the rest of the line verbatim. Confirm:
```bash
grep -c 'class="stamp">2026-07-12 · COMMIT' docs/diagrams/generation-flow.html
```
Expected: `0` (the literal `COMMIT` token is gone). Then `grep 'class="stamp"' docs/diagrams/generation-flow.html` should show the real hash.

- [ ] **Step 6: Self-contained / CSP check (no external references)**

Run:
```bash
grep -Eci 'src=|href="http|@import|url\(http|<script' docs/diagrams/generation-flow.html
```
Expected: `0` (no external assets, no `<script>`, no CDN — the toggle is pure CSS). Footnote `docs/design/…` mentions in the footer are plain text, not `href`, so they do not match.

- [ ] **Step 7: Full visual pass (the quality gate)**

Open the file. Systematically eyeball:
1. **Light + dark:** toggle OS/browser theme (or the artifact theme switch once published) — both render; starfield only in dark; chips legible in both.
2. **Toggle:** Flow ↔ Blocks switches cleanly, default is Flow, no flash of both, no console error.
3. **Flow:** four clocks → pipeline → seven phases (arrows + ↺ back-edge) → Cross-Cutting & Structural band → Inspection band. Markets densest.
4. **Blocks:** Genesis → L0 → L1 → Corps → Fleet → L2 → L3 → L4 → Actors → Interfaces → Determinism, with `provides →` labels and Owns/Reads/Provides rows.
5. **Status legibility:** green `IMPL`, gold `SPEC`, dashed-orange `GAP` are unmistakably distinct; gap cards have dashed borders; sub-lines colored to match.
6. **Layout:** no card text clipped, no element wider than the column, page body never scrolls horizontally (only `.chain-scroll` strips scroll internally on narrow widths).
7. **Stamp:** reads `2026-07-12 · <hash> · A–K roadmap closed; …`.

Fix any issue found, then re-run the relevant structural check from Steps 1–6.

- [ ] **Step 8: Commit**

```bash
git add docs/diagrams/generation-flow.html
git commit -m "chore(diagram): stamp commit, verify counts/consistency/no-stale-content"
```

---

## Task 9: Final review sweep and handoff note

**Files:**
- None modified (verification + handoff only).

**Interfaces:**
- Consumes: the completed file.

- [ ] **Step 1: Confirm the tree is clean and the file is committed**

Run: `git status --short docs/diagrams/generation-flow.html`
Expected: empty output (all changes committed).

- [ ] **Step 2: Re-assert the headline invariants one last time**

Run:
```bash
grep -c 'class="node mech' docs/diagrams/generation-flow.html; \
grep -c 'id="view-flow"' docs/diagrams/generation-flow.html; \
grep -c 'id="view-blocks"' docs/diagrams/generation-flow.html; \
grep -Eci 'src=|href="http|<script|prototype|TBD|TODO' docs/diagrams/generation-flow.html
```
Expected: `181`, `1`, `1`, `0`.

- [ ] **Step 3: Republish reminder (NOT a task action — a note for the orchestrator)**

This plan's job ends at a correct, committed HTML file. **Publishing the Artifact is done in the parent/orchestrator conversation, not here**, because the Artifact tool republish must run there with the stable URL. When the orchestrator picks this up, it should:
- Read the update procedure in project memory `generation-flow-artifact.md`.
- Publish via the Artifact tool passing `url = https://claude.ai/code/artifact/67f20b6b-4e8c-4941-b88b-fc071c1c64f4`, `favicon = 🌌`, and the unchanged title, so the living link stays stable.
- Then eyeball the published Artifact (the REPL/artifact eyeball acceptance gate).

Do not attempt the Artifact publish from within plan execution.

---

## Self-Review

**1. Spec coverage.** Every §4 subsection maps to tasks, and every one of the 96 nodes appears in Appendix A with an explicit Flow group and Blocks group:
- §4.1 Genesis (7) → Task 5 (`#blk-genesis`).
- §4.2 L0 (12) → Task 5 (`#blk-l0`) + Flow Tasks 3–4 for the phased ones, `#flow-crosscut` for l0b/l0f/l0g/l0l.
- §4.3 L1 (17) → Task 5 (`#blk-l1`) + Flow Tasks 3–4.
- §4.4 Corporations (6) → Task 6 (`#blk-corp`) + Flow Tasks 3–4.
- §4.5 Fleet (9) → Task 6 (`#blk-fleet`) + Flow Tasks 3–4, `#flow-crosscut` for fl1/fl2/fl3.
- §4.6 L2 (13) → Task 6 (`#blk-l2`) + Flow Task 4.
- §4.7 L3 (14) → Task 7 (`#blk-l3`) + Flow Task 4.
- §4.8 L4 (7) → Task 7 (`#blk-l4`) + Flow Task 4, `#flow-crosscut` for n7.
- §4.9 interfaces (4) → Task 7 (`#blk-interfaces`) + `#flow-crosscut` (all 4).
- §4.10 determinism (3) → Task 7 (`#blk-determinism`) + `#flow-crosscut` (all 3).
- §4.11 Inspection (4) → Task 4 (`#flow-inspection`).
Spec §2 two-view + toggle → Task 1. §3 three-state taxonomy → Task 1 CSS + per-node chip rule in Appendix A. §5 preserved CSS/clocks/pipeline/footer → Tasks 1–2. §5 retired items (4-state legend, PoC, design-pass pills, proto) → verified absent in Task 8 Step 4. §6 byte-identity → Appendix A single-source + Task 8 Step 3. §6 stamp → Task 8 Step 5. §6 "design tree governs" → Global Constraints + footer.

**2. Placeholder scan.** No "TBD/TODO/implement later/similar to". Every node card is fully written in its task. The one intentional literal placeholder is the `COMMIT` token in the header stamp, which Task 8 Step 5 resolves via `git rev-parse` (the plan explicitly cannot hardcode a commit). No "handle edge cases" style vagueness.

**3. Type/name consistency.** IDs are stable and reused verbatim across tasks: `#view-flow`, `#view-blocks`, `#tab-flow`, `#tab-blocks`, `.node.mech`, `.chip.{done,spec,gap}`, `.spec-item`/`.gap-item`, `.opr`, `.stack`, phase ids `#phase-{pc,mk,al,in,rs,id,ch}`, `#flow-crosscut`, `#flow-inspection`, block ids `#blk-{genesis,l0,l1,corp,fleet,l2,l3,l4,actors,interfaces,determinism}`. Chip labels are consistently `IMPL`/`SPEC`/`GAP`. The mech-card count is arithmetic-consistent: 29 (T3) + 60 (T4, incl. the 15-node crosscut band) = 89 Flow; +36 (T5) +28 (T6) +28 (T7) = 92 Blocks; total 181, re-asserted in Tasks 8 and 9. Both-view nodes are copied identically because both copies transcribe the same Appendix A row.
