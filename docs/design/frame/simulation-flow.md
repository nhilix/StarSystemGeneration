# The Simulation Flow — Seven Phases per Step

Two structural moves make the flow hang together.

**Move 1 — one controller touchpoint.** Decisions happen in exactly one phase
(Intent). Every controller emits two kinds of output there:

- **Standing policies** — budget weights, trade posture, diplomatic stances,
  military doctrine, shipbuilding priorities. Applied mechanically by *other* phases
  on subsequent steps.
- **Discrete acts** — declare war, offer alliance, charter a corporation,
  nationalize, found a colony, commission a fleet. Resolved this step.

Everything outside Intent is mechanical consequence. This is P2 with teeth: swapping
AI for player means swapping who answers one question — "given what you perceive,
what are your policies and acts?" — which is a 4X interface at polity scope, a
tycoon interface at corporate scope, and a character sheet at individual scope.

**Move 2 — decisions run on perception, consequences run on truth.** Phase 1 updates
each actor's believed world; Intent reads only that. Markets, battles, and migration
operate on actual state. The gap between the two is where stale-news drama lives
(P3).

## The phases

| # | Phase | What happens |
|---|---|---|
| 1 | **Perception** | News arrives (carried by traffic); each actor's perceived state (stances, reputations, known prices, known wars) updates, including its **capability brief** — its own economy read as rates (generation/yr, trailing income/yr, committed rates of in-flight work, free capability) plus the buildable candidates it perceives |
| 2 | **Markets** | Production (facilities + systems within port domains) → demand (population use-cases, industry, military, tech, plus in-flight construction pulling its per-year baskets) → **price formation per market** → trade flows route over the lane network under freight capacity (tariffs, blockades, sanctions constrain) → revenues, tax take |
| 3 | **Allocation** | Standing policies executed mechanically. In-flight **projects** advance first — each draws its per-year basket and wage stream, priority-ordered, starving to the fraction its inputs meet; completions fire (facilities commission, ports raise a tier, lanes open, hulls enter reserve, colonies found). Then plan entries whose start year has arrived **break ground** against truth into new projects. Upkeep, tech investment, corporate dividends, faction appeasement, mobilization ramps close it |
| 4 | **Intent** | The controller touchpoint: all institutions and role-holding characters emit policies + acts from perceived state — including the **standing plan**, a prioritized schedule of projects packed against perceived free capability across a fixed horizon |
| 5 | **Resolution** | Acts collide and resolve deterministically: port establishment (expansion), fleet movement and positioning, war fronts and battles, blockades established at port approaches, diplomacy matched (consent required both sides), annexations, capital falls |
| 6 | **Interior & demographics** | Within polities: cohesion, ideology drift, faction strength and pressure, succession (aging, death), **graduations** (schism / coup / charter), corporation-founding checks. Globally: population growth, famine, migration flows. New polities enter per the emergence schedule |
| 7 | **Chronicle** | Events finalized with world-years; news pulses emitted (arriving in future steps by distance and traffic); map residue updated (scars, zone inputs, throughput snapshots) |

## Ordering rationale

Perceive before deciding (P3); earn before spending (2→3); budgets constrain intents
(3→4); acts before consequences (4→5); interiors react to what just happened — a
lost war feeds faction anger this step (5→6); chronicle last so every phase's events
are captured and next step's news is this step's history (7→1).
