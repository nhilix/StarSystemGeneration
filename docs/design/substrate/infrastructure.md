# Infrastructure Vocabulary & Production

The catalog of buildable facilities, their siting rules, and how production runs
through them. Fifteen types in five families — the keystone port plus fourteen
buildable types — a closed, versioned vocabulary like the anchor types. Ownership, construction financing, condition, and destruction
lifecycles belong to the economy layer; wars make facilities objectives and
casualties.

## The catalog

| Family | Type | Produces / does | Siting rule |
|---|---|---|---|
| **Keystone** | **Port** (outpost → starport → nexus) | service radius, lane capacity, hosts the domain market, enforcement reach | the domain heart: best-connected system in the region |
| **Extraction** | Mine | Ore | belts, mineral-rich worlds (raster mineral richness + hex anchors) |
| | Skimmer | Volatiles | gas giants, ice worlds |
| | Agri-complex | Provisions, Organics | biosphere worlds (embodiment-relative richness) |
| | Excavation site | Exotics | exotics anchors (precursor sites, anomalies) |
| **Processing** | Refinery | Alloys, Fuel | near inputs or at ports |
| | Chemworks | Composites, Narcotics | near volatiles/organics supply |
| | Fabricator | Consumer Goods, Medicine | at population centers/ports |
| | Exotics lab | Refined Exotics | near excavation or high-tier ports |
| **Heavy** | Foundry | Machinery | alloy supply, developed domains |
| | Shipyard | Ship Components, hulls | orbital; wants port proximity + alloys |
| | Arsenal | Armaments | secure developed systems |
| | Compute core | Compute | exotics access, high-tier domains |
| **Support** | Depot | storage: each active tier extends the port's per-good stockpile capacity and multiplies decay down — the deep larder is built, not assumed | junction ports |
| | Fortress | defense, interdiction strength | port approaches, chokepoint lanes |
| | Gate | lane terminus: reach, capacity, transit speed by tier — one per lane end; no upkeep draw (sealed once linked, condition moves only by war damage) | port systems only; slot budget = port tier × GateSlotsPerPortTier (frame/space-and-travel.md §Lanes) |

Every facility has: a tier (1–3), a build cost in real goods (Alloys, Machinery,
Composites), a **construction time**, an upkeep draw, and a **hex anchor** — each
facility is a pre-commitment, so the hex a player visits shows the mine the
simulation built, at the tier it reached, damaged if it was raided (P1, P4). Build
cost and construction time are load-bearing together: a facility's construction
project draws `buildCost ÷ constructionYears` as its per-year basket and takes the
full construction time to commission ([../economy/assets-and-investment.md](../economy/assets-and-investment.md)).

**Raising a port a tier is itself construction**: a multi-year project drawing a
per-year basket (Alloys, Machinery, Refined Exotics, scaling with the target tier)
over the port-upgrade span — an exotics-poor realm stalls its nexuses for want of
the exotics chain, exactly as any construction stalls for want of its scarcest
input. The per-tier basket and span are knobs (`Expansion.PortUpgrade*` in
[../TUNING.md](../TUNING.md)).

## Production

Facility output per step:

```
output = base(type, tier) × terrain(raster fields at hex)
       × labor(domain population × embodiment affinity)
       × machineryGrade × automation(compute)
```

- **Terrain**: extraction reads the genesis fields at its hex — output *and grade*
  root in geography ([commodities.md](commodities.md)).
- **Labor**: drawn from the domain's population; Compute-driven automation
  substitutes (machine polities run thin-crewed industry; labor-rich low-tech
  polities cannot run advanced recipes at all).
- **Recipes**: processing facilities convert input stocks to output stocks; output
  grade per the Grade formula (recipe base × input blend × facility tier × tech).

**Organic baseline**: settled populations subsistence-farm and craft locally
without facilities — unserviced systems are poor, not starving-by-definition. The
baseline is small enough that facilities always dominate where they exist.

## P1 evidence

- **Legible residue**: facility icons and tiers on the atlas; domains visibly
  specialize (mining belts, breadbaskets, forge worlds, yard complexes); ruined
  facilities read as war/decline history at the exact hexes it happened.
- **Inhabitable state**: facilities are buildable, ownable, raidable, and
  garrisonable at play scope; a mine or yard is a place the player can dock at,
  work for, or seize.
