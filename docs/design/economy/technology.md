# Technology

The tech system: what advances, what it unlocks, and how it spreads. Every layer
consumes tech (grade ceilings, recipe gating, ship stat regions, port axes); this
document owns it.

## Four domains

Tech is per-polity, per-domain — not one scalar:

| Domain | Gates |
|---|---|
| **Industrial** | recipe variants, facility tiers, grade ceilings for industrial goods, automation level |
| **Military** | armament/warship stat regions (screens, point defense), fortification tiers, doctrine options |
| **Astrogation** | port service radius, inter-port range and lane speed, off-lane endurance ceilings |
| **Life** | medicine grade, agri productivity, population growth and plague resistance, augmentation (play clock) |

Each domain climbs a tier ladder (geometric investment thresholds). Tiers unlock
**ceilings and regions**, not flat multipliers — the qualitative ladder the Grade
system requires ([../substrate/commodities.md](../substrate/commodities.md)).

## Advancement

Research is an Allocation-phase execution of a standing policy: a research budget
split across domains, consuming **Refined Exotics** (the input) at a rate
multiplied by **Compute** (effective units) and modulated by temperament
composition and government form. No dedicated research facility exists — exotics
labs and compute cores upstream are the bottlenecks. Crossing a threshold emits a
`TechAdvance` event (domain and tier in the payload).

## Diffusion — laggards catch up

Three channels keep runaway leaders in check and make asymmetric emergence
survivable:

- **Trade contact**: passive drift toward trading partners' tiers, rate ∝ trade
  volume × both sides' openness, capped one tier below the source — you can learn
  from the goods you buy, but not lead with them.
- **Salvage and capture**: wreckage and captured facilities whose grade exceeds
  your ceiling grant progress in the relevant domain — battlefields and conquest
  are tech events, and precursor sites are the extreme case (artifacts above
  *any* ceiling grant the largest jumps, which is mechanically why everyone digs).
- **Espionage**: deferred with the intrigue substrate; the channel slot is
  reserved.

## P1 evidence

- **Legible residue**: per-domain tier panels; a tech map layer; `TechAdvance`
  chronicle events; visible capability asymmetry (whose ships have screens, whose
  ports reach farther).
- **Inhabitable state**: local tech determines the grades in shops, the ship
  marks for sale, and the port quality the player docks at; salvage and
  archaeology are tech gameplay at character scope.

## Provided interface

`Ceiling(polity, domain)` and `Region(polity, domain)` — consumed by the Grade
system, recipe gating, ship design sheets, and the port growth axes.
