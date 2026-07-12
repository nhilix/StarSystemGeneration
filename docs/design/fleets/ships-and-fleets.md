# Ships & Fleets

The fleet model — the physical carriers of goods, power, information, and
influence. A shared component consumed by the economy (freight capacity, piracy
risk) and inter-polity dynamics (combat vectors); owned by neither. Hulls are
conserved end-to-end (P4): built at yards through real production chains, lost to
attrition and battle as wreckage at real hexes.

## The chassis grid

Every design occupies a **role × size** cell:

| Role ↓ / Size → | Light | Medium | Heavy | Capital |
|---|---|---|---|---|
| **Freight** | courier-trader | hauler | bulk freighter | super-freighter |
| **Escort** | corvette | frigate | cruiser | — |
| **Line** | attack craft | destroyer | battlecruiser | dreadnought |
| **Carrier** | — | tender | fleet carrier | swarm-mother |
| **Scout / Courier** | scout | surveyor | expedition ship | — |
| **Colony / Seed** | — | pioneer | colony ship | seed-ark |
| **Special** | precursor and unique hulls, above-grid | | | |

**Designs are instantiated per polity.** A design = grid cell + stat sheet, derived
from species embodiment (machine minds: crewless swarm bias; hives: living capital
ships; lithics: dense armored slow hulls), culture/doctrine (militancy → line,
openness → freight), tech tier (which stat regions and cells are reachable), and
component grade. Designs drift along **lineages** over epochs — improved marks with
inherited names — so a fleet's composition reads as cultural history.

## The design sheet (two-layer stat model)

**Layer 1 — the sheet** (~15 stats per design; polities carry dozens of designs,
never thousands of sheets):

| Block | Stats | Distinguishes |
|---|---|---|
| **Combat** | strike power · sustained fire · tracking · armor · screens · point defense | line (armor+sustained) vs escort (tracking+PD) vs strike craft (strike+speed); swarms beat capitals unless screened |
| **Mobility** | lane speed · combat maneuver · off-lane endurance · fuel efficiency | couriers vs haulers; expedition hulls vs lane-bound bulk; who can flank through wilds |
| **Capacity** | cargo · hangar · berths | freighters vs carriers vs colony ships vs troop transports |
| **Operations** | sensors · signature · crew draw · automation · upkeep | scouts (sensors+endurance), smuggler hulls (signature), machine swarms (automation), militia vs professional navy (upkeep) |

Grade and tech act **per-stat**: component grade multiplies through the design's
emphasis (a high-grade escort gains disproportionate tracking and PD); tech tier
unlocks stat regions; precursor hulls hold values beyond current ceilings.
**Refit variants** carry module builds off the same hull — the Q-ship, the smuggler
compartment build, the customs cutter — sub-designs that double as the play-clock
outfitting system.

**Layer 2 — epoch aggregation.** Fleets aggregate composition into
combat/logistics **vectors** (strike, sustained, screening, tracking-vs-swarm,
detection, stealth, capacity, endurance-floor, upkeep). War resolution consumes the
vectors — rock-paper-scissors texture at aggregate cost. At play clock, the full
sheet is the ship the player flies (P7: one source of truth, two samplings).

## Production

Shipyards convert Ship Components (+ Armaments for warships, + Compute for advanced
designs) into hulls. A lay-down is a **hull-batch project** anchored at a yard: it
draws the recipe's goods as a per-year basket over the batch's build years and
commissions the hulls into reserve on completion, at the component grade
accumulated over the build. Yard tier caps how much batch work runs concurrently;
the standing plan schedules batches into that capacity, excess entries starting in
later years. Built → assigned to a fleet → lost to attrition/battle or scrapped
(partial alloy recovery). Every hull traces to an ore field through a 4-node chain.

## The fleet object

`(id, owner, location, composition, posture, commander, supply state)` — owner is
any institution; location is a hex or a route assignment; composition is hull
counts per design (+ mean grade); vectors compute on demand.

### Postures

| Posture | Does | Consumed by |
|---|---|---|
| **Posted** (route) | freight capacity on assigned lanes: Σ cargo × availability | Markets |
| **Escort** | screening/tracking counters piracy and interdiction on its route/convoy | Markets (risk), war |
| **Patrol** | legality enforcement in a domain: detection vs smuggler signature | black markets |
| **Blockade** | stationed at enemy port approaches; interdiction strain; contests lanes | war |
| **Expedition / Convoy** | the only moving posture: war fleets, colony convoys, ruin expeditions — travel is a duration (distance ÷ hull speed), so a colony convoy is in transit and interceptable for the world-years its voyage takes, founding on arrival | Resolution |
| **Reserve** | docked; minimal upkeep; readiness decays | mobilization |

### Movement and supply

Maneuvering fleets compose the three space-model leg types; off-lane legs gate on
the fleet's **endurance floor** (slowest hull limits the formation). Fleets draw
fuel and upkeep from their home port's market/stockpile; unsupplied fleets lose
readiness, then hulls. Supply convoys exist and raiding them is a posture
assignment, not a special rule.

### Attrition and wreckage

Losses conserve into **wreckage at the hex where they died**: salvage sites, and
after major engagements, the battlefield POIs the narrative layer compiles. Piracy
risk per lane = lawlessness × cargo value − escort vectors; it prices directly into
freight profit.

## Information carriage

News speed per lane = f(posted traffic frequency): busy lanes carry news fast,
backwaters slowly, wilds barely. Courier and scout fleets are deliberate
information assets; a player carrying news is this mechanic at individual scale.
Perception freshness derives from the traffic an actor's ports see — the news-speed
knob is emergent (P3).

## Commanders

Fleets above a prestige/size threshold take a **commander role** — a character
whose personality biases the posture AI (aggressive admirals push engagements,
cautious ones preserve hulls) and whose renown accrues through the chronicle.
Commanders age, die, are succeeded, and can defect; the role slot is defined here,
the drama consumed by the interior and war layers.

## P1 evidence

- **Legible residue**: fleet markers, posted-route ribbons, and wreckage sites on
  the atlas; named flagships and admirals in the chronicle; design lineages as
  visible cultural signatures.
- **Inhabitable state**: every posture is a player job — haul a posted route,
  escort a convoy, run customs, join a blockade, captain an expedition; the design
  sheet is the ship the player flies and refits.

## Provided interface

Freight capacity per route (Markets); combat/logistics vectors (war); colony
convoy mechanics (Resolution); traffic-derived news speed (Perception); commander
role slots (characters).
