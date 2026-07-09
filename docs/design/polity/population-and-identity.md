# Population, Identity & Migration

The people layer: who lives where, who they are, and how they move. Everything
inside a polity's politics stands on this substrate.

## The population segment

Population is a small set of **segments**:
`(species, culture, size, standard of living, ideology distribution)`. A domain
typically holds a dominant segment plus minorities — conquest, migration, and
diaspora add segments rather than blending them away. People are conserved and
their identity travels with them (P4).

**Segments are domain-level sim state.** Hex-level population is a *projection*
(Tier-3 settlement rolls plus facility staffing), never simulated — the one
exception is wilds settlements outside any domain, which are sparse hex-anchored
records. This pins the state model: population scales with domains (hundreds),
not hexes (hundreds of thousands).

**Segment income**: segments earn a labor share of the facilities they staff plus
organic-baseline subsistence (see [../economy/markets.md](../economy/markets.md));
income at local prices is what their demand bands clear, and SoL follows from
what they can afford.

## Demographics

Segment growth = f(SoL, provisions access, embodiment rate); famine and war shrink
segments. **Machine populations grow by manufacture**: their birth rate is fab
capacity consuming Machinery and Compute — a machine polity expands population as
fast as its industry allows, and cut off from industry it ages out rather than
starves. Species genuinely demograph differently.

## Culture — the slow layer

Cultures are registry entities: named, species-rooted, carrying the syllable
flavor that names systems, ships, and characters. They spread by migration and
conquest, blend very slowly under cohabitation, and **split** under long
separation — a frontier region diverges from the core over epochs until its
culture is genuinely distinct. Policy barely moves culture; repressing one
reliably produces resentment, not assimilation.

## Ideology — the fast layer

Four axes: **Authority↔Autonomy · Communal↔Individual · Open↔Insular ·
Sacral↔Material**. Segments carry a distribution over this space, drifting with
lived conditions: war → Authority; prosperity → Individual/Open; famine and
catastrophe → Sacral or Authority; corporate dominance → Individual/Material. A
polity's *official* ideology is population-weighted opinion filtered through
institutional inertia — the gap between them is where factions live
([factions-and-government.md](factions-and-government.md)).

## Migration

Per-step segment flows along gradients of SoL, safety, cultural affinity, and
opportunity (real wages from market prices), discounted by distance and lane
access. **Refugees** are the fast desperate variant, fleeing war and famine
wherever ships will take them. **Diasporas** are settled minority segments that
keep their culture and remember why they left (a stance input the narrative layer
reads). **Sentient trafficking** lands here mechanically: illicit flows moving
segments against the gradient toward labor-hungry, low-rights polities — rights
deriving from ideology and law code.

## P1 evidence

- **Legible residue**: culture and SoL map layers; migration flow ribbons;
  chronicle events for famines, exoduses, and cultural schisms.
- **Inhabitable state**: every port's demographic mix is visible texture —
  languages, tensions, who is rich and who serves; refugee convoys are
  encounterable objects; trafficking is a crime the player can fight or profit
  from.
