# Assets: Investment, Condition & Ownership

The lifecycle of infrastructure — completing the catalog defined in
[../substrate/infrastructure.md](../substrate/infrastructure.md). Wars make
facilities objectives and casualties; this document owns everything between
groundbreaking and ruin.

## Construction

Every piece of in-flight work — a facility rising, a port raising a tier, a gate
pair, a hull batch, a colony expedition, a mobilization — is a **project**: a
record of work that consumes goods and wages over world-years and completes on a
world-year, never within a step. A polity or corporation schedules projects in its
standing plan; Allocation breaks ground on a scheduled project when its start year
arrives, anchoring the site (P1 — the construction site exists before the facility
does, from the moment ground breaks) and validating against truth (site free,
treasury covers the administered value, slots open).

A project carries a **rate contract**, not a lump: its catalog `BuildCost` spread
across its `ConstructionYears` as a per-year basket of real goods (Alloys,
Machinery, Composites) plus a wage stream to the site's labor. Each Allocation the
project draws that basket **locally only**: the site's market shelf first, then
the site port's own larder where the funder owns the port — nothing teleports
in; remote goods arrive as shipments that land in the larder before the draw,
and a remote site starves at the pace of its last delivery. A gate pair draws
per end, half the pair's basket at each gate's own market and larder, the
scarcer end pacing the pair — half a highway opens no lane. It advances by
the fraction its scarcest input meets — a project fed 60% of its basket delivers
0.6 of a year's progress — so a starved work does not hoard, and its completion
year simply slides. Draws are **priority-ordered** against shared local inventory:
the war front and the flagship yard drink before the luxury starport, and the
starvation cascade falls out with no extra rule. A conserved-goods invariant holds
across the run — per-year basket × years-required equals the lump the work would
have cost as a single charge.

Completion fires the payload — the facility commissions and begins producing, the
port tier increments, the lane opens, hulls enter reserve, the colony founds — and
stages a chronicle event at its world-year. A project cancelled or abandoned
mid-work leaves its sunk goods sunk and its site an abandoned-works ruin (below).
Capture transfers a project at its current progress, like the facility it would
have become; the conqueror's next plan keeps or cancels it.

## Condition

Every facility carries **condition**: decays without upkeep, damaged by war and
raids, repaired by investment. Output scales with condition. Abandonment leaves a
ruin at the hex (residue with a date and an owner of record); capture transfers a
facility intact at its current condition.

## Ownership

Ownership transfers by sale, collateral seizure (default), nationalization, or
conquest — each a conserved ledger event with a chronicle record. State and
corporate ownership coexist in the same domain; the port's polity always taxes
what its market clears, whoever owns the mine.

## P1 evidence

- **Legible residue**: facility tiers and condition render at their hexes; ruins
  carry the date and cause of abandonment; ownership overlays show corporate vs
  state industry.
- **Inhabitable state**: construction sites, repair contracts, repossessed mines,
  and captured yards are all places and jobs at play scope.
