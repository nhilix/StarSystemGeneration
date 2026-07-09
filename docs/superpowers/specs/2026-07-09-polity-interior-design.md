# Polity Interior (Design Pass 4)

Status: **draft — awaiting user review**
Date: 2026-07-09
Parent: `2026-07-09-epoch-sim-master-frame-design.md` (pass 4). Builds on passes
1–3. Product docs (the interior stub split three ways):
`docs/design/polity/population-and-identity.md`, `factions-and-government.md`,
`characters.md`.

## 1. Overview

Everything inside a polity: population segments with a two-layer identity model
(slow culture, fast ideology), demographics and migration, factions with pressure
and the graduation internals, legitimacy/cohesion replacing flat schism odds, a
government-form catalog with succession rules, sparse on-demand characters with
dynasties and notables, and the **temperament composition** — the polity Intent-AI
personality as species × ideology × ruler × faction pressure, weighted by
government form. Resolves the inherited tickets: static species temperament and
conquest composition.

## 2. Decisions

- **Two identity layers** (over single blend and ideology-only): culture =
  slow identity (registry entities, syllable flavor, spreads/blends/splits with
  population movement, near-immune to policy); ideology = fast politics (four
  axes: Authority↔Autonomy, Communal↔Individual, Open↔Insular, Sacral↔Material;
  drifts with lived conditions). Yields cultural minorities distinct from
  ideological factions ("loyal foreigners, native rebels").
- **Population segments as the atom**: (species, culture, size, SoL, ideology
  distribution) per hex/domain; conquest composition = segment layering (P4).
  Machine populations grow by manufacture (Machinery + Compute), not provisions.
- **Migration as gradient flows** (SoL, safety, cultural affinity, real wages;
  refugee fast-path; diasporas retain culture and memory; trafficking as illicit
  counter-gradient flow toward low-rights polities).
- **Six faction bases** (ideological, cultural, regional, corporate, military,
  sacral); strength = population share + wealth (pass-2 dividends land) + patron
  characters; pressure as bounded policy pull + appeasement budget + grievance
  accumulation.
- **Graduation internals**: `strength × grievance` vs `legitimacy × enforcement`;
  schism (secession as new polity), coup (contested → civil war via the ordinary
  war machinery against a provisional polity), charter (pass-2 founding seen from
  inside), revolt (failed graduation, compounding grievance).
- **Legitimacy/cohesion model** replacing flat schism odds; conquest empires
  visibly accumulate their successor states.
- **Government-form catalog** (8 forms seated in ideology space × species:
  Autocracy, Collective, Assembly, Syndicate, Theocracy, Hive Unity, Machine
  Consensus, Steward Dynasty) setting succession, inertia, faction tolerance, and
  legitimacy source; forms change via graduation events.
- **Temperament composition**: species × official ideology × ruler personality ×
  faction pressure, weights per government form. Retires the fixed species
  temperament vector.
- **Characters sparse by construction**: role slots + event-born notables (war
  hero, founder, prophet, pirate lord, magnate, explorer; capped); deterministic
  on-demand generation; personality = ideology position + boldness/zeal/
  competence/ambition; species-real lifespans (hive-as-character, machine
  fork/deprecate); succession crises escalate through the political machinery;
  dynasties with prestige and cross-polity ties (a pass-5 diplomacy instrument);
  renown; biographies derivable from the event log (P8).

## 3. Testing Strategy

- **Invariants**: population conservation across migration/segment operations;
  ideology distributions valid; faction strength recomputable from bases; every
  graduation event leaves consistent registries (seceded domains reassigned,
  provisional polities resolved); succession always fills roles; character count
  respects sparsity caps; biographies reference only real events.
- **Unit tests**: ideology drift under condition exemplars; culture split after
  separation threshold; migration gradient math incl. refugee path; grievance/
  grip threshold behavior; each government form's succession rule; temperament
  composition weights per form.
- **Acceptance bands** (reference config): faction counts per polity; graduation
  event rates (schisms/coups/charters per 40 epochs); legitimacy distribution;
  character population per polity; migration volumes.
- **Goldens**: reference polity-interior snapshot (forms, factions, reigns).

## 4. Frame-Consistency Check (master frame §9)

Additions only. Graduation fills the cross-cutting mechanism with internals as
specified; the controller interface gains faction pressure as an input to the
polity AI without reshaping; Interior phase content matches the frame's phase 6;
characters use the role bridge as designed. No phase, taxonomy, or interface
reshape.

## 5. Deferred / Follow-Up (owners)

- Diplomacy uses of dynastic ties, war-cause wiring of faction pressure and
  succession claims, civil-war resolution details (pass 5).
- News/stance reactions to coups, revolts, trafficking scandals; biography and
  era rendering (pass 6).
- Spy networks / deep intrigue: state model supports it; consciously deferred.
- Election modeling depth for Assemblies (currently: ideology-lurch events at
  cycle boundaries).

## 6. Amendments to Prior Docs

- `docs/design/polity/interior.md` (stub) deleted, replaced by the three product
  docs.
- Prototype fixed species-temperament budget weights superseded on
  implementation by the temperament composition.
- Flow diagram: pass 4 pill → specced; stamp bump.
