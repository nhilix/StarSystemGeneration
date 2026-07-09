# Event Grammar, Chronicle & the POI Compiler

History as a readable — and mechanically active — product.

## The event grammar

One schema across all four clocks (P4's backbone):

```
(id, world-year, clock stratum, type, actors[], location, magnitude, valence,
 visibility, typed payload)
```

Types in eight families: cosmic, evolutionary, economic, political, military,
diplomatic, corporate, character. The Heron Merger, the Ore War, and an admiral's
death live in one append-only stream.

**Visibility** is load-bearing: *public* (emits a news pulse), *regional*
(locally known, spreads by contact), *secret* (no pulse — successful smuggling
runs, quiet deals; the substrate future spy systems need).

**Indexes** are views built over the log: per-place, per-actor, per-war,
per-character — the last is a biography for free.

## Chronicle views

All queries over the one log, at every zoom (P8):

- **Galaxy** — era-scale. **Era detection** clusters epochs by dominant event
  signature (expansion-heavy, war-dense, treaty-dense) and names eras from the
  participants' cultures. Eras are a recomputable annotation layer, never sim
  state.
- **Polity** — reign-by-reign arc: foundings, wars, reforms, schisms.
- **Character** — the biography index: born, rose, led, fell.
- **Place** — everything that happened *here*: the hex-tier annotation surface
  and the archaeology gameplay both read this.

## The POI compiler — incremental, live, epoch by epoch

The compiler runs **inside the Chronicle phase every epoch**, converting that
epoch's qualifying events into anchored POIs immediately. POIs exist from the
moment of creation and **influence the ongoing simulation** — the two-customer
test (P1) applies during genesis, not only at handoff:

| Source events | POI | Live sim effect |
|---|---|---|
| major battles | wreckage fields | **salvage value**: recoverable alloys/components at declining grade — a persistent profit niche (salvage corporations found themselves via the ordinary charter rule); depletes as recycled |
| sieges, razings, collapses | ruins, dead cities | lawlessness modifier (piracy havens); salvage; suppressed settlement |
| lost/razed capitals | ruined metropolis | **cultural claim anchor**: feeds irredentist tension and sacral pilgrimage |
| famines, atrocities | memorial sites | stance and culture memory anchors |
| precursor arcs (0b) | sites, dormant remnants | exotics extraction, hazard, research value — same mechanism, deeper stratum |

Every POI carries `(source events, type, magnitude, epoch, participants)` — debris
with a name, a date, and factions you can look up. The one-anchor-per-hex rule
arbitrates by magnitude; overflow becomes place-history annotation (nothing is
lost; it just doesn't pin a hex). POIs decay as their effects are consumed —
salvaged-out fields fade; the largest persist as permanent archaeology.

Artifact finalization therefore compiles nothing: it builds indexes and the
handoff. The map is always current at every epoch.

## P1 evidence

- **Legible residue**: this layer *is* the legibility half of the two-customer
  test — every event every pass emits becomes readable here, and POIs render at
  their hexes from the epoch they form.
- **Inhabitable state**: POIs are live content with the same meaning at both
  clocks — the salvage field a corporation strip-mines at epoch scale is the
  debris field a player picks through at play scale.
