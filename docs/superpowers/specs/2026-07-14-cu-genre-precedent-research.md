# Slice CU research thread 3 — genre precedent: how multi-actor grand strategy games handle money supply (2026-07-14)

Phase 1 research for Slice CU (currency & minting redesign), per
`docs/superpowers/plans/2026-07-14-slice-cu-currency-kickoff-prompt.md`
research item 3. This thread covers **only** the "precedent within games
this project already draws on" ground, plus a brief survey of other
multi-actor economy games, and does not re-cover:

- The SFC/Godley-Lavoie stock-flow-consistent framing, the sovereign-
  issuance mechanism, or the "some sector has to be a reliable net
  spender" reasoning — all already covered in
  `docs/superpowers/specs/2026-07-13-monetary-equilibrium-design.md`.
- The deliberate 2026-07-09 decision to keep a single abstract credit
  with no currency depth — `docs/superpowers/specs/2026-07-09-markets-wealth-corporations-design.md`
  §5: *"Currency/monetary depth (multiple currencies, exchange,
  inflation): consciously out — the single abstract credit stands unless
  a future pass proves need."* This slice is precisely the "future pass"
  that decision anticipated; this doc supplies the "need" evidence.
- The Eurozone/ECB and EVE Online/ISK precedents — those are research
  thread 2's territory per the kickoff (real-world currency-union theory
  and single-issuer MMO faucet design), cited here only where they sharpen
  a comparison.

## 1. Victoria 3

**No per-nation currency at all — a single abstract unit, explicitly by
design choice, not oversight.** All prices and treasuries in Victoria 3
are denominated in a single reference unit ("pounds sterling") regardless
of which country holds them. There is no franc, no mark, no exchange rate
between nations — the wiki and dev-diary material treat this as a
deliberate simplification, reasoned (per the community thread) from the
game's own historical setting: 1836–1930 major currencies were gold-
pegged with effectively fixed exchange rates until Bretton Woods (1944,
after the game's timeframe), so modeling FX would buy realism the period
doesn't reward and would require "a completely separate exchange system
on top of the markets" that posters and (implicitly) the developers judged
not worth the complexity.

Crucially, **this does not make Victoria 3 a shared-currency-with-
independent-minting system** — the "one pound" unit is a *display/ledger
convention*, not a pooled money supply multiple actors mint into. Each
country's treasury is its own isolated stock:

- **Sources**: gold mines (literal money production from the ground) and,
  overwhelmingly, ordinary production/trade revenue — buildings collect
  income according to goods produced and cleared at market price. Tax
  collection from the state's own pops and enterprises.
- **Sinks**: building upkeep, wages, government spending, military
  maintenance.
- **Reserve/credit mechanism**: a national gold reserve (cap ≈ 20% of
  annual GDP, diminishing returns above it) absorbs surplus; a deficit
  first draws down the reserve, then — once the reserve is empty —
  automatically becomes sovereign debt against that *one country's own*
  credit limit, at that country's own interest rate. Default (breaching
  the credit limit) pauses construction and penalizes offense/defense/
  throughput for that country alone, lifted once its own budget returns
  to balance.

Every one of these mechanisms — minting, taxing, borrowing, defaulting —
is scoped to a single national economy. Nothing in Victoria 3 lets one
country's gold mine, deficit, or default touch another country's ledger.
The "shared unit label" is cosmetic; the money supply itself is as
partitioned as if each country used a different currency.

Sources: [Currencies in Victoria 3 (Paradox Forums)](https://forum.paradoxplaza.com/forum/threads/currencies-in-victoria-3.1475088/), [Treasury (Victoria 3 Wiki)](https://vic3.paradoxwikis.com/Treasury), [Exploring the Economic Engine of Victoria 3](https://riverlimburg.substack.com/p/exploring-the-economic-engine-of), [Victoria 3 Dev Diary #12 — Treasury](https://forum.paradoxplaza.com/forum/threads/victoria-3-dev-diary-12-treasury.1488588/)

## 2. EU4

**Nominally one universal unit ("ducats"), transferable 1:1 between
nations at face value — but each nation's ducat has its own, separately-
tracked real purchasing power via a per-nation inflation stat.** This is
the more interesting and more directly relevant precedent, because on the
surface it looks closer to this sim's model than Victoria 3 does: ducats
*do* move between independent national treasuries at par — subsidies,
peace-deal reparations, mercenary hire, trade — with no FX conversion
step. If you stopped the analysis there, EU4 would look like exactly the
"shared fungible currency across independent actors" pattern this sim
currently has.

It does not, once you look at how inflation works:

- **Inflation is a per-nation stat, not a galaxy/world-wide one.** Each
  country tracks its own inflation percentage independently.
- **Inflation is caused by that nation's own money-creation choices**:
  taking or renewing a loan (+0.10 inflation each time, loan size capped
  at roughly half the nation's own development — an endogenous, self-
  sized borrowing limit, notably similar in spirit to this sim's
  receipts-scaled issuance cap), realizing treasure-fleet gold (inflation
  proportional to that windfall's share of the nation's own income),
  debasing currency (converting development directly into ducats, at a
  corruption cost), or direct gold-mine production.
- **Inflation's effect is entirely local**: it raises *that nation's own*
  costs — construction, fort upkeep, ship repair, advisor costs, state
  maintenance — as a flat percentage surcharge, with no corresponding
  income boost. It is reduced only by that nation's own spending (an
  administrative-power action) or its own Master of Mint advisor.

The consequence: a ducat sitting in a low-inflation nation's treasury
buys measurably more of that nation's own goods/services than a ducat in
a high-inflation nation's treasury, even though the two ducats are
nominally identical and freely transferable at 1:1. EU4 achieves the
*appearance* of one fungible currency (no FX friction on transfers) while
actually modeling **N separate real-value currencies that happen to share
a display unit and a frictionless transfer mechanism** — every nation's
own profligacy is a private cost it pays through its own rising prices,
never exported onto any other nation's ledger. There is no mechanism by
which Nation A's debasement or treasure-fleet minting raises Nation B's
costs. This is the load-bearing structural difference from this sim's
`Credits`, where one polity's `IssueSovereignCredit` call dilutes the
purchasing power of every other polity's balance identically, because
there is no per-polity price level to decouple them.

Sources: [Economy (EU4 Wiki)](https://eu4.paradoxwikis.com/Economy), [Economic reworks: mercantilism, inflation, debase currency (Paradox Forums)](https://forum.paradoxplaza.com/forum/threads/economic-reworks-mercantilism-inflation-debase-currency.975433/), [What is ducats? (Steam Community)](https://steamcommunity.com/app/236850/discussions/0/1696048245845966620/)

## 3. Other multi-actor economy games (brief survey)

- **Stellaris** — Energy Credits are the universal in-game unit label,
  but each empire's economy (production, trade-route conversion to
  energy credits at its own capital, market buy/sell) is internal to that
  empire. Cross-empire transfer happens only through explicit, bounded
  diplomatic/trade actions (subsidies, trade deals, market listings at a
  galactic market clearing price) — never a shared pool any empire can
  unilaterally inflate for another. Same partitioned-treasury pattern as
  Victoria 3, with a shared unit label as in EU4.
- **Crusader Kings 3** — Gold is tracked per-ruler/per-realm; when a
  realm splits, the resulting independent realms each get their own
  separate treasury going forward. No shared pool across independent
  rulers at all; not even a common display unit is load-bearing since
  realms don't transact through a market the way Vic3/EU4/Stellaris
  nations do.
- **Distant Worlds 2** and **Aurora 4X** — both scope "credits"/"wealth"
  strictly to a single empire's own treasury, funded by taxing that
  empire's own private economy; no cross-empire shared pool or minting
  interaction appears in either game's documented economy model. (Both
  are also primarily single-empire-perspective games — the player's own
  empire's books are the whole visible economy — so they're weaker
  precedent for the *multi-actor* question than Vic3/EU4/Stellaris, but
  they land on the same partitioned-treasury norm where they do model
  other empires' economies at all.)

Sources: [Stellaris Trade (Fandom Wiki)](https://stellaris.fandom.com/wiki/Trade), [Distant Worlds 2 Economy Guide](https://game.lb-product.com/en/games/distant-worlds-2/guides/distant-worlds-2_economy-guide), [Wealth (AuroraWiki)](http://aurorawiki.pentarch.org/index.php?title=Wealth), [Trade System (AuroraWiki)](https://aurorawiki.pentarch.org/index.php?title=Trade_System)

## 4. Implications for this sim

**Answering the kickoff's explicit question**: no genre precedent checked
here — Victoria 3, EU4, Stellaris, Crusader Kings 3, Distant Worlds 2,
Aurora 4X — actually models N independent fiscal actors minting into and
spending from one truly fungible, undifferentiated shared pool the way
this sim's `Credits` currently works. Every one of them partitions the
money supply per actor, by one of two structural means:

1. **Literal separate currencies/treasuries** (Crusader Kings 3, Distant
   Worlds 2, Aurora 4X, and structurally also Stellaris/Victoria 3 despite
   a shared *display* unit) — one actor's money creation or destruction
   never touches another actor's balance or purchasing power at all.
2. **A shared unit label with frictionless nominal transfer, but a
   per-actor real-value overlay that re-partitions the economics anyway**
   (EU4's per-nation inflation stat) — the closest any of these games
   comes to "one shared currency," and it still stops well short of this
   sim's model, because debasement's cost is captured entirely by the
   debasing nation's own rising prices, never exported to anyone else.

This sim's current design — every polity mints unilaterally into the
exact same `Credits` pool, with no per-polity price level, no FX, and no
partition of any kind — has **no precedent in this survey at all**, genre
or otherwise (research thread 2 should be consulted for whether real-
world currency-union theory or EVE's ISK fare any better; this thread's
answer, scoped to grand-strategy/4X genre precedent specifically, is a
clean no). The near-universal norm is "give every independent fiscal
actor its own money supply" — full separation being far more common than
EU4's partial one. Read against research item 3(a)/(b)/(c) in the
kickoff's Phase 2 framing:

- **Option (a) — per-polity currencies with FX** is the Victoria-3/
  Stellaris/CK3/Distant-Worlds/Aurora norm, i.e., the single most
  genre-conventional answer, not the exotic one; its cost (a real new FX
  subsystem) buys the sim actual precedent-backed convergence.
- **Option (b) — one shared currency, single galaxy-wide issuer** has no
  clean genre precedent surveyed here (no game examined lets a *shared*
  pool exist without also partitioning it structurally or via a
  per-actor stat) — real-world central-bank theory (currency unions) is
  the stronger source for this option, per research thread 2, not
  grand-strategy game design.
- **Option (c) — keep per-polity minting into one pool, add discipline**
  is the option this survey gives the least genre support for. EU4 is
  the *only* game here with anything resembling "actor mints, actor
  bears a consequence," and even EU4 makes that consequence strictly
  local (per-nation inflation), never shared — the one thing option (c)
  would need to replicate the closest available precedent is exactly the
  per-polity price-level/inflation mechanic this sim does not currently
  have (`Money.Supply` is one number, not N numbers). Option (c) without
  that addition has, per this survey, no working precedent to point to.

**One caution against over-reading this**: this survey is grand-strategy/
4X genre convention, not proof any specific option is *correct* for this
sim — research thread 1 (monetary-union theory) and thread 2 (EVE/MMO
precedent) weigh the same three options from different angles, and the
brainstorm should weigh all three together, not let this thread's genre
count alone decide. What this thread does settle: "one universal
currency, unilaterally mintable by every independent actor, with no
partition of any kind" is not a pattern this project can point to any
cited genre precedent for — the Eurozone comparison in the kickoff prompt
undersold it, if anything: Victoria 3, EU4, Stellaris, CK3, Distant
Worlds 2, and Aurora 4X all give every independent fiscal actor either a
fully separate money supply or (EU4's case) a per-actor real-value
overlay on a shared label. The sim's current design is the outlier among
its own cited genre precedents, not merely under-disciplined within a
known-good pattern.
