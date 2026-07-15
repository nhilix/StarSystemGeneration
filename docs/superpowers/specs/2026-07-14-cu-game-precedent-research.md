# Slice CU — Game Precedent Research: Single Shared Currency at Scale

Research spec, phase 1 thread 2. Feeds the Slice CU (currency & minting redesign)
brainstorm. Not a design doc — no decisions here, just sourced findings.

## Why this research

Slice ME gave every polity independent, unilateral minting authority over
`Credits`, a single universal currency shared by every polity, corporation,
and market in the galaxy. The acceptance sweep showed 83-97% of final money
supply was fiat-issued, with every polity's printing diluting every other
polity's credits and no coordinating mechanism. The project has repeatedly
cited EVE Online's ISK as precedent for "a single universal currency is fine,
look at EVE" — but that claim has never been checked past the surface level.
This research checks it.

## 1. EVE Online ISK

### 1.1 What creates ISK (faucets)

ISK, EVE's single galaxy-wide currency, is not minted by any in-universe
actor. It is generated exclusively by the game server crediting players for
completing NPC-driven (PvE) activities. The EVE University wiki and CCP's own
Monthly Economic Report (MER) breakdown identify the recurring faucet
categories as:

- **Bounty prizes** — ISK paid for killing NPC ("rat") pirates, historically
  the single largest faucet category. Payout amount is server-set per NPC
  type/region, not player-set.
- **Mission/agent rewards** — ISK (plus loyalty points, standings) paid by
  NPC agents for completing PvE mission objectives.
- **Incursion payouts** — ISK for group PvE against the NPC Sansha's Nation
  faction.
- **Insurance payouts** — a player can pre-pay a premium to insure a ship;
  if it's destroyed, CCP's NPC insurance system pays out based on hull value.
  Historically the payout has exceeded what premiums collected would justify,
  so insurance nets out as a faucet on top of being a sink (see 1.2) —
  which is exactly why CCP has repeatedly nerfed insurance payout ratios
  over the game's life (2015 "hull insurance" changes cut payouts).
- **"Commodities"** — a MER-tracked faucet category (planetary interaction /
  industry-adjacent NPC buy orders) that became the dominant faucet after
  bounty nerfs (see 1.3); the public reporting doesn't fully define its
  scope beyond "commodities," so treat that category as a black box.

None of these are triggered by a player, corporation, or NPC faction
*deciding* to create ISK. They are automatic server payouts for completing
content the server defines. This is the central structural difference from
Slice ME's model, covered in section 4.

### 1.2 What destroys ISK (sinks)

- **Transaction/sales tax** — a percentage skimmed on every player-to-player
  market sale.
- **Broker fees** — a fee to place a market order at all, independent of
  whether it fills.
- **Contract fees** — fee to create a player contract.
- **Insurance premiums** — the sink side of the faucet/sink pair in 1.1.
- **Skill injectors / skill extraction economy** — CCP sells "PLEX" and
  skill-related items for real money; ISK-denominated purchases of some
  services (e.g., contract broker relists) are pure sinks.
- **NPC-sold items / reprocessing / manufacturing costs** — ISK spent buying
  from NPC sell orders, or lost to industry job costs, leaves circulation.
- **Clone costs / jump clone fees** and similar recurring NPC-charged fees.
- **Structure/citadel fuel and reinforcement costs.**

Sinks are consistently mechanisms where ISK is paid *to the server* (an NPC
counterparty), never to another player — player-to-player payment is a
transfer, not a sink.

### 1.3 How CCP actually tunes the balance

- CCP publishes a **Monthly Economic Report (MER)**, produced by an
  in-house data-science/economics function inside CCP internally referred
  to by the community as "CCP Quant" (a rotating role/handle, not one
  person across the game's life — Dr. Eyjólfur "Eyjó" Guðmundsson was the
  original in-house economist; CCP Quant and later CCP Data Science
  continued the function). The MER reports total money supply, ISK
  faucets/sinks by category, destruction value, and Chained-Laspeyres
  price indices across a basket of ships/modules/implants/skills/ammo
  /drones/boosters/fuel.
- The tuning mechanism is direct dev-side numeric adjustment: CCP changes
  bounty payout tables, insurance payout ratios, tax/broker-fee percentages,
  and NPC buy/sell prices, then watches the next MER for effect. There is no
  in-universe policy lever — it's a config change on CCP's side, shipped as
  a patch note.
- A documented example of tuning-in-action: the **2018 Dynamic Bounty
  System (DBS)**. Null-sec "ratting" bounty payouts had become the dominant
  ISK faucet; CCP made per-system bounty payouts decay with sustained
  ratting activity in that system (down to as low as ~30% of face value)
  to push players toward more contested/PvP-active space. This is CCP
  unilaterally changing a faucet's shape by fiat in response to MER data
  showing bounties as an oversized, undifferentiated source. After the
  nerf, the MER's "Commodities" faucet category grew to fill the gap —
  i.e., even CCP's own tuning shows faucets are fungible/whack-a-mole, not
  a solved equilibrium.
- Community secondary sources (Adam4EVE sinks/faucets tracker, the *Ancient
  Gaming Noob* and *Nosy Gamer* blogs, which have tracked the MER for over
  a decade) note **persistent data-quality problems** in the MER itself —
  e.g. late-2025 reporting where CCP's own published sink/faucet chart was
  off by roughly 9% against the actual money-supply change, and CCP's
  public commentary on the numbers reportedly doesn't always match the
  MER's own figures. Takeaway: even the entity with full server-side
  visibility and unilateral tuning authority has visibly imperfect
  measurement of its own single-currency economy in practice.
- CCP also applies an **"Active ISK Delta"** adjustment in the money-supply
  calculation to account for ISK removed via anti-RMT (real-money-trading)
  enforcement — i.e., ISK literally deleted from banned accounts. This is
  a sink CCP applies out-of-band, by administrative fiat, not through any
  in-game mechanism.

### 1.4 Has any in-universe actor ever had legitimate minting power?

**No.** Every source checked — EVE University wiki, CCP's own support docs,
community economic trackers, and forum/community history — describes ISK
creation as strictly a server-side NPC payout mechanism. Players, player
corporations, alliances, and NPC factions have never been given a sanctioned
way to create ISK; the closest thing to "npc factions creating money" is
that bounty/mission payouts are *framed* diegetically as coming from NPC
factions (CONCORD, agents, Sansha's Nation drops), but mechanically it's the
server minting ISK on completion of a defined activity, not a faction with
a discretionary budget or policy lever.

The only cases of players creating ISK outside sanctioned faucets are
**exploits/bugs**, treated as bannable offenses, not features:
- A ~2005-2008 exploit chain generated an estimated 2.5–3 trillion ISK
  before being patched and its users banned.
- A player-owned-starbase (POS) reactor exploit (discovered ~2009) let
  players harvest materials that shouldn't have existed, with an estimated
  upper-bound impact around 6.7–12 trillion ISK; 134 accounts were banned,
  two corporations implicated were also engaged in real-money-trading ISK
  sales.

CCP's response to both was bans plus currency clawback, not legitimization.
This is a strong signal: EVE's single-currency model has never actually
been tested against multiple independent actors with discretionary minting
power, because EVE was never designed to allow that — the one time it
happened by accident (exploits), it was treated as economy damage to be
reverted and punished, not a coordination problem to be solved. **This is
the crux for Slice CU**: EVE is not proof that "one shared currency +
multiple independent minters" works. It's proof of the opposite scenario —
one shared currency + exactly one minter (the server) + zero legitimate
alternative mints — working. Slice ME's design (every polity is a legitimate,
sanctioned mint) has no EVE analogue at all.

## 2. Other single/shared-currency persistent economies

### 2.1 Old School RuneScape (OSRS) — gold, Grand Exchange

- OSRS gold (GP) is a single currency shared by the whole player population,
  with a centralized auction-house-style market (the Grand Exchange, GE).
  There is no player-faction minting; gold enters via NPC monster drops,
  quest rewards, and skilling activity, structurally identical in kind to
  EVE's server-side faucet model (single centralized minter: the game).
- OSRS's own wiki and Jagex's own dev commentary acknowledge the game
  **historically lacked a reliable gold sink or item sink** — high-value
  loot (especially from instanced boss content, where drop rate is
  per-kill-attempt rather than population-gated) can enter the game faster
  than there are sinks to remove it, causing long-run inflation.
- Jagex's 2024-era "Economy — Future Plans" rework introduced a **Grand
  Exchange tax** (a flat percentage, since raised from 1% to 2%, waived
  under 100gp, capped at 5,000,000gp on high-value sales) explicitly
  designed as a gold sink, with the collected tax revenue used by Jagex to
  buy back and *delete* specific overabundant items from the game — i.e.,
  a sink whose rate is manually tuned by the developer based on estimated
  daily item/gold inflow, functionally the same lever CCP uses in EVE
  (dev-set numeric knob, tuned reactively against observed inflow data).
- Relevant to Slice CU: OSRS shows that even with just NPC-drop faucets and
  a single central developer as tuner (the simplest possible case), a
  persistent-economy game can still run gold-sink-negative for years before
  the developer intervenes — underscoring that "faucet/sink balance" is a
  continuously fought battle even under centralized control, not a solved
  problem to import wholesale.

### 2.2 Path of Exile — currency-as-items, no single currency at all

- PoE deliberately has **no single "gold" currency**. Trade is conducted
  via a barter economy of 20+ distinct orb/scroll items (Chaos Orbs,
  Exalted Orbs, Divine Orbs, etc.), each of which also has a functional
  crafting use, so "currency" items are consumed by ordinary player crafting
  activity — crafting itself *is* the sink, deterministically, without a
  developer needing to design a separate tax/fee sink.
  Historically Chaos Orbs, more recently Divine Orbs, function as the de
  facto "base unit" purely through emergent player convention, not developer
  designation.
- This is the strongest **counter-example** to a single universal currency:
  PoE's designers avoided the single-currency-inflation problem entirely by
  making the currency itself scarce, consumable, and multi-purpose rather
  than a pure accounting token. Directly relevant if Slice CU ever considers
  "should Credits even be one fungible token" as an option.

### 2.3 Albion Online — silver, explicitly player-driven economy

- Albion's economy is described by its own community/dev commentary as
  80-90% player-driven (nearly everything is player-crafted, player-traded,
  and destroyed by players), sharing one currency (Silver) galaxy-wide,
  closest analogue among these examples to "many independent economic
  actors, one currency, some sinks are player-facing decisions" (e.g. guild
  territory/island upkeep is a recurring cost decided by player-controlled
  guilds, though the *rate* is developer-set, not guild-set).
  Full-loot PvP is a major structural sink: gear is permanently destroyed
  (not just transferred) on death, which is a sink built into moment-to-moment
  play rather than a tax.
- Community feedback threads (forum "Silver Inflation — No Silver Sink",
  2020s) show a **recurring, unresolved player complaint** that faucets
  (mob kills, dungeons, chests, the NPC "Black Market") outpace sinks
  (repair costs, island upkeep, market tax, mail tax), causing chronic
  inflation — i.e., even with per-player minting via ordinary gameplay
  (structurally similar to bounty/mission faucets) and no delegated
  authority to any faction, a single shared currency at population scale
  still drifts toward oversupply by default. This mirrors Slice ME's
  finding almost exactly, despite Albion having no polity-level minting
  actor at all — suggesting the oversupply tendency is closer to a
  population-scale-faucet problem than a specifically "who gets to mint"
  problem.

## 3. Cross-cutting observations

- **Every example checked (EVE, OSRS, Albion) uses exactly one minter: the
  game server**, tuned by exactly one developer-side authority, with sinks
  that are dev-tunable numeric knobs (tax %, fee flat amounts, payout
  tables) rather than actor-controlled policy. Even PoE, which avoids a
  single currency altogether, still has the server as sole faucet (drop
  tables) — no game here delegates minting to an in-universe faction.
- **None of these games have ever given a player-facing entity (corporation,
  alliance, guild, NPC faction with agency) a sanctioned mechanism to create
  currency.** The one case where it happened outside sanction (EVE's
  duping/reactor exploits) was treated purely as a bug to patch and punish,
  never folded into the design.
- **Chronic oversupply is the norm, not the exception, even under fully
  centralized single-minter control.** OSRS and Albion both show
  years-long dev-vs-inflation fights despite total centralized authority
  over both faucets and sinks. This suggests Slice ME's 83-97%-fiat-issued
  finding is not solely a "we let polities mint" problem — a
  single-minter version of this sim could still oversupply if faucet
  design (however it's redefined) isn't matched to sink design turn over
  turn. Fixing *who* mints doesn't automatically fix *how much*.
- **Tuning in every precedent is continuous and reactive, not solved once.**
  CCP, Jagex, and Albion's devs all iterate against observed
  supply/inflation data on an ongoing cadence (monthly MER, forum-driven
  patches). If Slice CU's design borrows this pattern at all, it implies
  a recurring instrumented feedback loop (something like the sim's own
  MER-equivalent) rather than a single fixed emission-rate formula tuned
  once at design time.

## 4. Implications for this sim

1. **The EVE precedent the project has cited does not actually transfer.**
   EVE's single-currency model works (to the extent it does) *because*
   there is exactly one minter — the server — with zero legitimate
   in-universe alternative. Slice ME's every-polity-mints design has no
   structural EVE analogue; citing EVE as precedent for "one currency, many
   independent minters" is citing a game that has never allowed that
   combination even by accident without treating it as a bannable exploit.
   Any redesign that keeps a single universal `Credits` currency needs a
   *different* precedent than EVE for the "many legitimate mints" part
   specifically, because none of the surveyed games has one.
2. **If polities keep unilateral minting authority, the sim needs an
   EVE/OSRS/Albion-style sink layer that scales with total mint volume**,
   not just per-polity local sinks — every precedent's sinks are
   population-wide percentage/fee mechanisms (tax, fees, upkeep,
   destruction), not counterparty-specific. Consider whether Credits needs
   a galaxy-wide transaction tax or decay mechanism analogous to GE
   tax/broker fees, independent of which polity minted the credits being
   spent.
3. **Consider whether "one shared fungible currency" is the right frame at
   all.** PoE's item-currency model (each currency unit has consumptive
   utility, so ordinary play is the sink) is the one surveyed design that
   structurally avoids the oversupply-by-default pattern seen everywhere
   else. If polities are meant to have economically distinct identities,
   a PoE-style or multi-currency-with-consumptive-use model may fit a
   polity-vs-polity setting better than forcing continued convergence on
   a single fiat token shared with zero coordination, as EVE-style single
   fiat currencies assume a single controlling authority this sim
   explicitly does not have.
4. **Faucet/sink imbalance is likely to recur regardless of the minting-
   authority fix**, per the Albion/OSRS evidence that centralized,
   single-minter economies still drift toward oversupply for years without
   active intervention. Whatever Slice CU lands on, budget for an ongoing
   tuning mechanism (sim-health-harness-style instrumentation, per the
   ME/SH slices already in place) rather than treating this as a one-time
   emission-formula fix.
5. **If any sanctioned player/polity-facing minting authority is kept at
   all, this sim would be a genuine novel design point** — no surveyed
   precedent (EVE, OSRS, PoE, Albion) has ever given that power to an
   in-universe actor on purpose. That's worth flagging explicitly to the
   user as a design decision without an off-the-shelf model to lean on,
   not a gap to research further — the research on this specific question
   came back empty across every title checked.

## Sources

- EVE University Wiki, ["Making Money"](https://wiki.eveuniversity.org/Making_Money)
- CCP/EVE Online support docs: ["ISK and PLEX"](https://support.eveonline.com/hc/en-us/articles/14141550499612-ISK-and-PLEX), ["Currencies"](https://support.eveonline.com/hc/en-us/articles/14216227951388-Currencies)
- Adam4EVE, [MER Sinks/Faucets tracker](https://www.adam4eve.eu/mer_sinks_faucets.php?avg=7)
- The Ancient Gaming Noob, MER commentary series, e.g. [January 2025 MER](https://tagn.wordpress.com/2025/02/21/the-january-2025-eve-online-monthly-economic-report-destruction-and-reflections/), [April 2025 MER](https://tagn.wordpress.com/2025/05/12/the-april-2025-eve-online-monthly-economic-report/), [December 2025 MER / 2025 destruction summary](https://tagn.wordpress.com/2026/01/12/the-december-2025-eve-online-monthly-economic-report-and-a-summary-of-overall-destruction-in-2025/), [May 2026 MER](https://tagn.wordpress.com/2026/06/17/the-may-2026-eve-online-monthly-economic-report-and-how-much-isk-is-too-much-isk/)
- The Nosy Gamer, [November 2025 MER — money supply commentary](https://nosygamer.blogspot.com/2025/12/eve-onlines-november-2025-monthly.html), [MER methodology changes](https://nosygamer.blogspot.com/2025/09/changes-are-coming-to-eve-onlines.html)
- Talking in Stations, [CCP Quant on EVE Economic Reports](https://www.talkinginstations.com/2017/05/tis-4-30-17-ccp-quant-on-eve-economic-reports/)
- EVE Online dev blog / news, ["Monthly Economic Report - March 2017"](https://www.eveonline.com/news/view/monthly-economic-report-march-2017), ["EVE Economy Update - EVE Vegas 2015 Report"](https://www.eveonline.com/news/view/eve-economy-update-eve-vegas-2015-report)
- Imperium News, ["Bounty System Enhancements & Overhauls"](https://imperium.news/bounty-system-enhancements-overhauls/), ["CCP Releases Charts, Details on EVE Economy"](https://imperium.news/ccp-releases-charts-details-eve-economy/)
- The Ancient Gaming Noob, ["CCP Relents on the Dynamic Bounty System for Unknown Reasons"](https://tagn.wordpress.com/2022/10/25/ccp-relents-on-the-dynamic-bounty-system-for-unknown-reasons/)
- Engadget, ["Rumored four-year, multi-trillion ISK exploit in EVE Online"](https://www.engadget.com/2008-12-11-rumored-four-year-multi-trillion-isk-exploit-in-eve-online.html), ["CCP Games releases findings on EVE starbase exploit investigation"](https://www.engadget.com/2009-02-10-ccp-games-releases-findings-on-eve-starbase-exploit-investigatio.html)
- Ten Ton Hammer, ["EVE Online's POS Exploit Exposed"](https://www.tentonhammer.com/articles/eve-online-s-pos-exploit-exposed)
- OSRS Wiki, [Sink (economy)](https://oldschool.runescape.wiki/w/Sink_(economy)), [Gold sink](https://oldschool.runescape.wiki/w/Gold_sink), [Economy](https://oldschool.runescape.wiki/w/Economy)
- RuneScape/Jagex, ["Old School Economy - Future Plans"](https://secure.runescape.com/m=news/a=415/old-school-economy---future-plans?oldschool=1), [Update log](https://oldschool.runescape.wiki/w/Update:Old_School_Economy_-_Future_Plans)
- GameBoost, ["Jagex's New OSRS Grand Exchange Tax and Item Sink Changes"](https://gameboost.com/blog/osrs-grand-exchange-tax-item-sink-changes)
- Path of Exile Wiki (Fandom), [Currency](https://pathofexile.fandom.com/wiki/Currency), [Trading](https://pathofexile.fandom.com/wiki/Trading)
- PoE Wiki, [Currency](https://www.poewiki.net/wiki/Currency)
- Game Developer, ["Path of Exile Economy: Currency Trading"](https://www.gamedeveloper.com/design/path-of-exile-economy-currency-trading)
- Albion Online Wiki, [Silver](https://wiki.albiononline.com/wiki/Silver)
- maenmiu, ["Albion Online's mail tax – the ultimate silver sink"](https://maenmiu.com/albion-onlines-mail-tax-the-ultimate-silver-sink/)
- Albion Online Forum: ["Silver Inflation - No Silver Sink"](https://forum.albiononline.com/index.php/Thread/208795-Silver-Inflation-No-Silver-Sink/), ["Albion Online Economy Needs Attention"](https://forum.albiononline.com/index.php/Thread/216315-Albion-Online-Economy-Needs-Attention/), ["Albion 2025 How Are You Dealing with the Silver & Market Madness?"](https://forum.albiononline.com/index.php/Thread/216393-Albion-2025-How-Are-You-Dealing-with-the-Silver-Market-Madness/)

*Note on source quality:* a few surface-level summaries (FasterCapital.com
articles) surfaced in search results are low-quality SEO content, not
authoritative — they were cross-checked against EVE University wiki, CCP's
own docs, and long-running community economic trackers (Adam4EVE, Ancient
Gaming Noob, Nosy Gamer) before being relied on here. The insurance
faucet-vs-sink characterization in particular should be treated as
reasonably-confident-but-not-CCP-primary-sourced; CCP's own MER category
breakdown for insurance specifically wasn't accessible during this pass
(the Adam4EVE sinks/faucets tracker returned a database error at fetch
time — worth retrying if this needs to be load-bearing for a design
decision).
