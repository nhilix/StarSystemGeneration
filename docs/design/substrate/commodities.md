# Commodities, Demand & Grade

The L0 goods layer: what exists to be produced, wanted, and traded — and the Grade
system that carries quality through every chain. Value comes only from use-cases
(P4): every good below exists because something consumes it.

## The vocabulary — 17 goods, three tiers, chains 1–4 nodes deep

### Raw (extracted/grown; terrain-derived from the genesis fields)

| Good | Source terrain | Notes |
|---|---|---|
| **Provisions** | biospheres (embodiment-relative) | directly consumed — subsistence |
| **Ore** | mineral-rich cells, belts (cosmic enrichment) | the industrial root |
| **Volatiles** | gas giants, ice worlds (gas-rich regions) | fuel & chemistry feedstock |
| **Organics** | rich biospheres | fibers/biomass/compounds — a second export for garden worlds |
| **Exotics** | precursor sites, anomalies | scarce by design; raw form |

### Processed (one step; some directly consumable)

| Good | Recipe | Consumed by |
|---|---|---|
| **Alloys** | Ore (+Volatiles) | construction, shipbuilding staple |
| **Fuel** | Volatiles | lane transit, off-lane endurance, military ops — movement is never free |
| **Composites** | Volatiles + Organics | plastics/chemical derivatives → Consumer Goods, Components, habitation |
| **Consumer Goods** | Organics + Alloys/Composites | standard-of-living, directly consumable |
| **Medicine** | Organics (+Exotics advanced) | SoL; famine and plague mitigation |
| **Narcotics** | Organics/Composites | elastic pleasure demand; legality varies by jurisdiction |
| **Refined Exotics** | Exotics | tech investment input; high-tier recipe ingredient |

### Capital (2–3 processing steps; the most valuable)

| Good | Recipe | Consumed by |
|---|---|---|
| **Machinery** | Alloys (std) · Alloys + Refined Exotics (adv) | facility construction/upkeep; productivity multiplier |
| **Ship Components** | Alloys + Machinery (std) · +Refined Exotics (adv) | shipbuilding (deepest chain: Ore→Alloys→Machinery→Components) |
| **Armaments** | Alloys (std) · Alloys + Refined Exotics (adv) | fleet weapons; war consumption |
| **Compute** | Refined Exotics + Composites | tech-rate multiplier; automation (labor substitution); advanced ships; machine-population consumption |
| **Luxuries** | Organics + Exotics | elastic, high-margin, prestige — the corporate niche good |

**Multiple recipes per output.** Where a good lists recipe variants, the standard
variant is exotics-free (mass over quality) and the advanced variant is exotics-gated
(higher grade base, more effect per unit). Tech tier gates which variants a producer
can run. Exotics scarcity therefore reads as *capability asymmetry*, not just a
deficit number — and exotics wars have a motive beyond balance sheets.

## Grade

Every stock of a good is `(quantity, grade)`, grade ∈ [0,1] — one scalar carried
wherever stocks live (markets, stockpiles, cargo holds, and depletable body
reserves — a Mine/Excavation body holds a finite `(good, quantity, grade)` stock
it is dug out of over time until the rock runs dry).

**Origin — grade flows from geography through the chains:**

- **Raw** goods inherit grade from terrain: cosmic enrichment sets ore purity;
  biosphere richness sets provisions/organics quality. Rich cells yield *better*,
  not just more.
- **Processed/capital** grade = recipe base × input-grade blend × facility tier ×
  producer tech tier.
- **Tech tier is the grade ceiling** — the tech ladder is qualitative, not just
  multiplicative. **Precursor artifacts occupy grades above any current-era
  ceiling**: mechanically why ruins and dormant remnants are prizes worth wars.

**Blending & consumption:** stocks mix by quantity-weighted mean grade; consumption
draws at the mean (no FIFO bookkeeping). Storage does not change grade;
perishability/decay is a stockpile concern owned by the economy layer.

**Effect — one interface for every use-case:**

```
Effective(useCase) = quantity × GradeMultiplier(useCase, grade)
```

Armaments grade multiplies military strength per unit; Machinery grade multiplies
productivity; Components grade sets ship quality; Consumer Goods and Medicine grade
multiply SoL per unit; Compute grade multiplies tech/automation rate; Luxuries grade
multiplies prestige and margin.

**Grade × price — value density:** markets price effective units, so high-grade
goods carry more value per hull-ton. Long-distance trade self-selects for high grade
(worth the fuel); low-grade bulk stays regional — the luxury-lane/local-staples
pattern emerges from arithmetic, not rules.

**Grade × war:** quantity-versus-quality forces are real — mass at grade 0.4 versus
half the hulls at grade 0.85 — and neither dominates by default.

**Display:** continuous internally; shown as named bands (crude / standard / fine /
advanced / masterwork / precursor-grade) so map, chronicle, and shops speak one
language.

## The demand model

Demand sources, in priority order when budgets are tight:

1. **Population — three bands**, embodiment- and culture-modulated:
   *subsistence* (Provisions — unmet means famine), *standard of living*
   (Consumer Goods, Medicine — SoL feeds growth, legitimacy, migration pull),
   *luxury* (Luxuries, Narcotics — elastic, prestige-driven). Lithics eat little but
   demand more machinery; machine populations consume Fuel, Machinery, and Compute
   instead of Provisions and Medicine — species economies genuinely differ.
2. **Industry**: facility construction and upkeep (Alloys, Composites, Machinery).
3. **Movement**: Fuel — every lane hop and off-lane crossing costs fuel;
   blockade-running is expensive in goods, not just time.
4. **Military**: fleet construction (Components) and war upkeep (Armaments, Fuel).
5. **Technology**: Refined Exotics investment; Compute multiplies the rate.

Exotics are pulled by tech, Components, Medicine, and Luxuries simultaneously while
supply concentrates at a handful of sites: genuine scarcity, genuine imports,
genuine exotics wars.

## Legality — a polity policy, not a property of the good

Every polity's law code marks each good *legal / restricted / prohibited* (plus
tariff levels). Prohibition converts demand into **black-market demand** at high
margins, servable only through smuggling channels (off-lane legs, corrupt low-tier
ports). "Illicits" are therefore jurisdiction-relative: Narcotics are legal in the
syndicate freeport and banned in the theocracy; Armaments trafficking is Armaments
flowing where they're prohibited. Cartel corporations, pirate factions, and smuggler
players get a real economy from the same goods plus off-lane physics.

**Sentient trafficking** is modeled distinctly — an illicit *population flow*
(moving people against their will toward labor-hungry, low-rights polities), not a
stockpiled good. It is crime against the population substrate, flagged as such, and
the most reputation-damaging trade in the news/stance system.

## Play-clock projection

The generational sim tracks these wholesale categories; play-scope items are
**retail instances of the same goods**: personal equipment ← Armaments/Consumer
Goods; cybernetic and genetic augmentation ← Medicine + Compute; habitation ←
Machinery/Composites; personal and larger vessels ← Ship Components; career-shaping
goods (licenses, charters, berths) are services priced off the same markets.

Retail generation at any location **samples the local market's (good, grade,
quantity) stocks**: a frontier port with grade-0.3 armaments sells crude rifles; the
imperial capital's grade-0.9 stocks yield masterwork gear; exceptional items are
seeded tail-samples around the local mean — a legendary blade can turn up on the
rim, rarely. Availability, quality, and price of personal-scale items all project
from wholesale state with no extra sim bookkeeping (P7 applied to goods).

## P1 evidence

- **Legible residue**: economy map layers show dominant production and grade
  shading; chronicle entries name what wars and booms were about (an ore rush, an
  exotics grab, a narcotics corridor); high-grade regions read as visibly better
  fitted-out.
- **Inhabitable state**: every good is something a player can haul, refine, corner,
  smuggle, or fight over; retail shops project from real wholesale stocks; recipe
  variants and grade give industrialist players real decisions.

## Provided interface

- The goods table (ids, tiers, recipes and variants, use-case list).
- `Stock = (good, quantity, grade)` and `Effective(useCase)` — consumed by markets,
  war resolution, SoL, tech, and retail projection.
- Demand profiles: per population segment (species/culture/SoL band) and per
  institutional use-case.
- Legality schema: per-polity law code over goods (legal/restricted/prohibited +
  tariff), consumed by markets, smuggling, and relations.
