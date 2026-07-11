# Full-Design Acceptance Pass — Slice J (2026-07-11)

The certification sweep of `docs/design/` against the implementation at the
end of slice J (branch `slice-j-handoff`), per the roadmap's row J. Every
P-number certified or the gap filed; remaining perfect-info/stub comments
hunted down (all fixed in-branch). This is a decision record: the gap list
below is the backlog the design tree already promises, in one place.

## P-number certification

| P | Verdict | Evidence |
|---|---|---|
| **P1** two-customer | **Certified** | Every mechanic has a REPL surface (map layers, panels, chronicle prose) and inhabitable state; the handoff view (`threads`) delivers the world in motion; POIs live from the epoch they form. |
| **P2** replaceable controllers | **Certified** (character scope partial) | `HandoverTests`: wrapping every controller mid-run is byte-invisible; a scripted player takes a polity throne and a corporation board mid-run and its acts resolve by the same rules; load reattaches stock controllers. *Partial*: character personal acts are declared but unarmed (gap 1) — the character-scope slot exists structurally, with nothing to say yet. |
| **P3** perception explicit | **Certified** (one filed exception) | Controller inputs run through compressed belief (strengths, menus, fronts, books) at news speed; stances move at arrival; regional word spreads by contact age-crossing. *Exception*: freight arbitrage clears on true prices as Markets-phase resolution — perceived-price arbitrage is gap 2, and perception-and-news.md was amended in-branch to say so. |
| **P4** conservation & causality | **Certified** | Hull ledger (built = active + wrecked + scrapped, wrecks at real hexes) holds through fine tick (`FineTickTests`); credits conserve through wages, taxes, dividends, loans, nationalization, salvage; every dramatic outcome traces through the log (per-place/actor/war/character indexes). |
| **P5** emergence over paint | **Certified** | Corporations, pirate bands, POIs, eras, precursor residue, cultures, borders all derive; the two slice-J wires (ruins lawlessness, memorial anchors) closed the last decorative POIs. |
| **P6** determinism & artifact | **Certified** | Byte-identity at both tick resolutions; keyed stateless rolls (channels 0–73); versioned layer sections; config artifact-stamped; hex tier never persisted; LoadThenContinue at coarse and fine. |
| **P7** world-time rates | **Certified this slice** | `Sim.GenerationYears` split the calendar unit from the integration step; every persisted clock converted to world-years; per-generation intensities scale by `StepFraction`; the fine-tick band suite caught and killed two real violations (price demand-vs-stock, yard-slot truncation). Residual coarse/fine divergence is keyed-roll path divergence, bounded by the certified bands. |
| **P8** story at every zoom | **Certified** | Eras (galaxy), reign-by-reign polity chronicle, biography index (`bio`), place view (`chronicle place`), per-war index (new this slice). |

## Per-plane verdicts

- **frame/** — conform. Seven phases in order, one controller touchpoint,
  decisions-on-perception/consequences-on-truth, actor taxonomy, two-plane
  space model, derived territory, four clocks. `KnownPolityIds` is a
  vestigial roster (nothing consumes it; comment fixed).
- **genesis/** — conform. Cosmic field stack + features, biosphere loop,
  causal emergence schedule with contact bonus, precursor arcs with typed
  residue, machine descendants, dormant remnants.
- **substrate/** — conform with gaps 5–6. 17 goods, grade end to end,
  demand bands, legality/black books, 15-type catalog with siting,
  construction time (`ConstructionYears`), organic baseline.
- **economy/** — conform with gaps 2–4, 11. Market step order, re-export
  demand, household income/labor share, loans + collateral default, tech
  domains + diffusion, corporate lifecycle (founding → influence → death).
- **polity/** — conform with gaps 7–8. Segments, machine manufacture
  growth, ideology axes, migration/refugees/diasporas, six faction bases,
  graduation table, eight government forms, temperament composition,
  sparse characters, dynasties, six notable types, plagues.
- **fleets/** — conform with gap 10. Chassis grid (no Carrier role in
  practice), design lineages/marks, vectors, six postures, supply,
  wreckage, traffic-derived news speed, commanders.
- **interpolity/** — conform with gap 9. Contact + pre-heard stances,
  warmth/tension sources, treaty ladder, federation (overlap discount),
  vassalage (both exits), dynastic instruments, native policies, casus
  belli menu, theater/objective war, settlements, annihilation wars.
- **narrative/** — conform (perception-and-news.md amended in-branch to
  the landed per-subject snapshot model). Pulses/journeys, belief layer,
  reputation, eras, POI compiler with live effects, salvage, handoff
  (contents, resumability, handover, never-closing log, delta boundary).

## The gap list (filed, deliberate, in one place)

1. **Unarmed contract acts** — 11 act records exist with no Resolution
   path: `SanctionAct`, `CharterAct`, `ProcurementContractAct`,
   `CharterApplicationAct`, `MajorAcquisitionAct`,
   `RelocateHeadquartersAct`, `PatronizeFactionAct`, `DefectAct`,
   `RoleResponseAct`, `MarryAct`, `LeadExpeditionAct`. The stock AIs never
   issue them, so P2's symmetry holds vacuously; they are the player-verb
   backlog for the live game. (Armed today: found-colony, declare-war,
   treaty, settlement-response, nationalize, vassalage, dynastic
   instrument, quarantine.)
2. **Perceived-price arbitrage** — freight plans on true prices; belief
   carries no partner prices. The trader's stale-price edge awaits the
   live game (design amended to say so).
3. **Procurement contract objects** — escrowed, news-propagated contracts
   (markets.md §3) stand in as mechanical stockpile-target procurement.
4. **Sanctions** — no lane-legality closure mechanism (the act is gap 1;
   the machinery markets.md §Sanctions describes is absent). Corporate
   re-flagging evasion goes with it.
5. **Sentient trafficking** — unmodeled (commodities.md flags it as
   distinctly modeled; population-and-identity.md lands it in migration).
6. **Perishability** — reserves store loss-free (markets.md §Stockpiles).
7. **Culture drift** — cultures mint at schisms and emergences only;
   separation-split and slow blending (population-and-identity.md
   §Culture) are undone.
8. **Plague depth** — excavation-release outbreaks, Medicine-goods
   mitigation (only the Life-tech tier discounts mortality today), plague
   memorial POIs, and a plague era-signature are unmodeled.
9. **War depth** — occupation objectives and defensive mirrors, raidable
   supply-line objectives, imposed-legality settlements, commander
   *boldness* posture bias (competence is wired), personal unions.
10. **Fleet depth** — Carrier role and refit variants unused; piracy risk
    is not priced into freight profit (pirate bands stay registry-level —
    the deliberate H/I/J choice; J1 wired their *founding* to ruins).
11. **Automation** — the production formula accepts Compute-driven
    automation; Markets passes 0.0 (labor substitution unarmed).
12. **Courier fast-paths** — news travels traffic only; couriers, scout
    dispatches, and the news-carrying player are play-clock content.
13. **Espionage** — reserved by design (technology.md, characters.md);
    not a gap, recorded for completeness.

## Stub-comment hunt (all fixed in-branch)

`ControllerContract.cs` (view/RelationBrief/WarBrief headers,
`KnownPolityIds`), `ColonyValuation.cs` (the capital's-own-view decision),
`MarketEngine.MoveFreight` (true-price standing choice + gap pointer),
`SimState.Cultures` (splits exist since G/H), `Phases.InteriorPhase`
(slice-F schedule landed). Remaining "stub"/"slice X arms" mentions in
comments are historical notes about retired code, verified accurate.
