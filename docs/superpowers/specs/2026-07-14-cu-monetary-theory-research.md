# CU Slice — Monetary Theory Research: Multi-Actor Currency Unions

**Date:** 2026-07-14
**Slice:** CU (currency & minting redesign) — Phase 1, research thread 1
**Type:** Research record (not a design decision). Feeds a later brainstorming phase with the user.

## Framing the question

Slice ME (monetary equilibrium) fixed a treasury-spiral bug by granting **every**
polity **independent, unilateral minting authority** over `Credits` — a **single
universal currency** shared by every polity, corporation, and market in the galaxy
(no exchange rates, no per-polity currency separation anywhere in the codebase). On
the committed acceptance sweep this produced **83–97% of the final money supply as
fiat-issued**, with each polity's printing diluting every other polity's holdings and
**zero coordinating mechanism** between them.

The user flagged this as structurally unsound by analogy to the Eurozone, which
specifically forbids member states from unilaterally printing euros — only the ECB
expands the shared-currency base. A currency union with N sovereign fiscal actors
sharing ONE currency is exactly this sim's situation.

The prior ME design doc (`2026-07-13-monetary-equilibrium-design.md`) already covers
the **single-actor** SFC / Godley-Lavoie / Kalecki-identity "some sector must be a
reliable net spender" reasoning thoroughly and correctly. **This document does NOT
re-cover that.** The question here is strictly the **multi-actor** one: how do systems
where multiple distinct fiscal/political actors share one currency stay stable when no
single actor unilaterally controls the money supply? Real monetary theory and history
have a lot to say, and the verdict is fairly one-directional.

---

## 1. The Eurozone / EMU — the primary real-world case

The euro is the closest real-world analogue to this sim: ~20 sovereign fiscal actors,
one shared currency, no exchange rates between them. Its architecture is a deliberate
answer to exactly the failure mode the ME sweep exhibits, and it rests on **four
distinct mechanisms**, not one.

### 1a. ECB monopoly on base-money issuance (the core rule)
Member states **cannot** create euro base money. Issuance of the monetary base is a
monopoly of the ECB / Eurosystem; national central banks operate only as agents under
ECB decisions. This is the single most important structural fact: **the sovereign
fiscal actors are deliberately separated from the money-printing lever.** They can tax,
spend, and *borrow*, but they cannot mint. This is the direct inverse of the sim's
current "every polity mints unilaterally" design.

### 1b. No-bailout clause (Maastricht Treaty Art. 125)
No member state (nor the Union) is liable for another's debts. The intent: force each
state to face a hard budget constraint via bond-market discipline, since it can neither
print nor be rescued. Notably, this clause was **inserted at Germany's insistence**,
precisely out of fear that a shared currency without it becomes an "uncontrollable
transfer union." (During the 2010–12 crisis it was substantially watered down — see 1e.)

### 1c. Fiscal discipline rules — the Stability and Growth Pact (SGP)
Because removing the printing lever does **not** by itself remove the incentive to
free-ride via debt, the EMU layered on **explicit fiscal rules**: the SGP (1997,
again at Germany's insistence) caps public deficits at 3% of GDP and debt at 60% of
GDP, with financial penalties for violators. The 2012 Fiscal Compact (Treaty on
Stability, Coordination and Governance) added balanced-budget requirements and
automatic correction mechanisms, ratified by all Eurozone members.

The crucial lesson: **the designers judged centralized issuance alone to be
insufficient** and deliberately bolted fiscal-discipline rules on top. Centralized
minting stops direct dilution; it does not stop a member from over-borrowing against
the shared currency and exporting the risk to everyone else.

### 1d. TARGET2 — the "who owes whom" settlement ledger
TARGET2 is the Eurosystem's real-time gross settlement system. Cross-border euro flows
accumulate as **central-bank claims and liabilities**: Germany's Bundesbank is a net
creditor (>€1tn at peak); Italy (~€670bn), Spain (~€484bn), Greece (~€106bn) are net
debtors. TARGET2 is best read as a **standing mutual-clearing ledger that makes
imbalances explicit and settleable** rather than letting them vanish into anonymous
dilution. Interpretation is debated (a "stealth bailout" of the periphery vs. a mere
symptom of capital-flow reversals — the Richmond Fed and BIS lean to the latter), but
for our purposes the design-relevant point is: **a shared-currency system builds an
explicit bilateral ledger of net flows between actors.** The sim currently has no such
ledger — printing is anonymous and untracked across polities.

### 1e. What happens when a member is reckless anyway — Greece 2010–12
The textbook stress test. Greece borrowed heavily in the 2000s, **used fraudulent
accounting to hide deficits far above the 3% rule**, and in late 2009 disclosed the
true numbers, triggering the crisis. Key findings:

- The SGP was **not credibly enforced** — when France and Germany themselves breached
  the 3% limit in 2003 they were not penalized, gutting the pact's credibility years
  before Greece.
- The no-bailout clause was **abandoned in practice**: bilateral loans → the temporary
  EFSF → the permanent European Stability Mechanism (ESM), plus ECB interventions
  (OMT). The union chose de-facto mutualization over letting the rule bite.
- Conclusion drawn across the literature (Minneapolis Fed, NBER, Fordham ILJ): the
  EMU's institutional design for enforcing fiscal discipline was **inadequate** —
  monetary centralization without a fiscal union or credible enforcement left a gap that
  a single reckless actor could exploit.

**Design takeaway from the Eurozone:** centralized issuance is necessary but the real
system also needed (and struggled to enforce) fiscal-discipline rules AND an explicit
inter-actor settlement ledger. Removing the print lever is step one, not the whole
answer.

---

## 2. Historical currency unions WITHOUT central control — and how they died

The clearest evidence that **distributed minting authority self-destructs** comes from
pre-central-bank currency unions. The Latin Monetary Union is the canonical case, and
it maps almost exactly onto the sim's current design.

### The Latin Monetary Union (1865–1927)
France, Belgium, Italy, Switzerland (later Greece, Papal States, others) agreed to a
common bimetallic standard and **mutually accepted each other's coins** — but **each
sovereign kept the right to mint.** No central issuer. This is structurally the sim's
"every polity mints the shared currency unilaterally." It failed through several
mutually reinforcing mechanisms:

- **Debasement race / seigniorage theft.** A member could mint a coin with *slightly
  less* precious metal, spend it at full face value across the union, and pocket the
  difference — i.e. **extract seigniorage at the whole union's expense.** The **Papal
  States** did exactly this from 1866 (administrator Antonelli issued under-fineness
  silver), flooding other members with debased coin until expelled in 1870 owing 20M
  lire. **Greece** repeatedly reduced the gold content of its coins; expelled 1908,
  readmitted 1910. This is the debasement analogue of the sim's fiat-dilution problem:
  **unilateral issuers offload the cost of their issuance onto every other member.**
- **Gresham's law / adverse selection.** "Bad money drives out good." When silver's
  market price fell, higher-content coins were hoarded/melted and only the debased coins
  circulated. Arbitrageurs (notably German traders) minted cheap silver into union coin
  and swapped it for gold. The union's *good* money systematically drained out.
- **Uncontrolled paper issuance.** The LMU **failed to outlaw paper money**; France and
  Italy printed banknotes to fund their own spending, **forcing other members to bear
  part of the cost** — again the exact fiat-dilution externality the sim shows.
- **Contagion of one member's instability.** Greece's political/fiscal instability
  spread strain to the whole union (echoing Greece 2010).

**This is the direct historical precedent for the ME sweep result.** A multi-sovereign
shared currency with distributed minting and no central issuer produced: seigniorage
theft, a debasement race, adverse selection, and cost-externalization — and it did not
survive. It limped on de jure to 1927 but was functionally dead by WWI.

Other unions reinforce the pattern: the **Scandinavian Monetary Union (1873–1914)** and
the **German Zollverein/pre-1871 monetary arrangements** ultimately consolidated toward
a **single central issuer** (the Reichsbank, 1876) to hold together — the historical
arc runs *from* distributed minting *toward* centralization, essentially never the
reverse.

---

## 3. Does the literature ever endorse a currency union with NO central issuer?

Short answer from the search: **no credible endorsement of a stable, distributed-issuer
currency union with no coordinating mechanism.** But the literature is *not* silent on
alternative stabilizers short of a full single sovereign issuer.

### Optimal Currency Area (OCA) theory — Mundell (1961), McKinnon, Kenen
OCA theory frames a shared currency as a **cost-benefit trade**: members gain lower
transaction costs but **lose the exchange rate as a shock-adjustment tool.** Stability
then requires *substitute* adjustment channels — Mundell: labour/factor mobility;
McKinnon: trade openness; Kenen: diversified production + **fiscal transfers / risk
sharing.** OCA theory is fundamentally about *how a shared currency survives shocks*,
and its recurring answer is **shared fiscal/risk-pooling machinery**, not distributed
independent issuance. It assumes a single monetary authority and asks what *fiscal*
structure must accompany it. The mainstream reading of the euro is that it is **not a
full OCA** precisely because it lacks the fiscal union OCA theory says it needs.

### Where the literature *does* allow non-single-sovereign designs
The relevant nuance: "no single *central bank*" is fatal; "no single *fiscal actor*"
is fine and normal (the euro has 20 fiscal actors and one central bank). Credible
stabilizing alternatives to a unitary sovereign issuer that appear in the literature:

- **A shared/federated central issuer owned by the members** — the Eurosystem itself is
  federated: 20 national central banks + the ECB, decisions centralized but ownership and
  seigniorage distributed by a **capital key** (see §4). This is "distributed governance,
  centralized issuance decision." It is the workable middle path, not fully unitary.
- **State-contingent seigniorage-sharing rules** for full risk-sharing within one
  currency area (OCA risk-sharing literature) — issuance stays centralized but the
  *proceeds* are redistributed by rule.
- **Collective caps / rules-based issuance** — the SGP is exactly this genre: members
  keep fiscal sovereignty but accept **binding numeric limits** enforced by a common
  authority. The catch, per §1e, is **enforcement credibility**.
- **Mutual clearing / settlement systems** — TARGET2, and historically Keynes's proposed
  International Clearing Union / bancor, make imbalances explicit and settleable rather
  than anonymized. This is a coordinating mechanism that does not require a single
  sovereign to *own* the currency.

**Bottom line for the design question:** the literature treats **some** coordinating
mechanism as effectively mandatory. Pure independent unilateral minting by each actor
with no cap, no shared issuer, no clearing ledger, and no seigniorage-sharing is the one
configuration with **no theoretical defender and a clear historical death record (LMU).**
It does **not** insist the coordinator be a single omnipotent sovereign — a federated
issuer, rules-based collective caps, mutual clearing, and seigniorage-sharing formulas
are all recognized stabilizers. So "make distributed minting survivable with added
discipline rules" is a defensible design direction *if* at least one real coordinating
mechanism is added; "keep unilateral minting with nothing added" is not.

---

## 4. Seigniorage and how real currency unions allocate it

**Seigniorage** = the issuer's profit from creating money: the interest earned on the
assets held against the (non-interest-bearing) money it issues. In a shared currency it
is the natural flashpoint, because whoever issues **captures** it while everyone shares
the dilution. This is the precise wealth-transfer channel behind both the LMU debasement
race and the sim's 83–97% fiat result.

How the Eurosystem neutralizes the fight:

- **Issuance income is pooled, not kept by the issuer.** NCBs' monetary income is pooled
  and **redistributed in proportion to the ECB capital key** — so no member profits from
  issuing "more." This structurally removes the incentive to over-issue for private gain.
- **The capital key** is each member's share of ECB capital, set by **average of the
  member's share of EU population and share of EU GDP, in equal measure**, revised every
  5 years. 8% of banknote value is allocated to the ECB; the other 92% to NCBs by capital
  key.
- **This is redistributive.** Literature ("Eurowinners and Eurolosers," Sinn et al.)
  notes socializing legacy seigniorage wealth creates windfalls for low-monetary-base
  members and losses for high-base ones (Germany, Netherlands, Austria) — i.e. the
  sharing formula itself is a live political-economy variable, not neutral.

**Why it matters here:** any sim mechanism that keeps multiple issuers *must* decide who
captures the seigniorage. If the issuer keeps it (status quo), you get the LMU/ME
dilution race. If it is pooled and redistributed by a fixed key (Eurosystem), the
incentive to over-issue for private advantage disappears and issuance becomes a
collective-policy question instead of a unilateral land-grab.

---

## Implications for this sim (considerations, not a recommendation)

The actual design decision belongs to a later brainstorming phase with the user. These
are the load-bearing considerations the research surfaces:

- **Unilateral multi-actor minting with zero coordination is the one configuration with
  no theoretical defender and a concrete historical death (LMU 1865–1927).** The ME
  sweep's 83–97% fiat-dilution result is the textbook symptom, not a tuning artifact.
- **"Central issuer" is not the only fix, but *some* coordinating mechanism appears
  effectively mandatory.** The literature offers a menu: (a) a single/federated issuer
  that monopolizes minting; (b) rules-based collective caps on issuance à la SGP; (c) a
  mutual-clearing ledger (TARGET2) that makes inter-polity imbalances explicit and
  settleable; (d) seigniorage pooled and redistributed by a fixed capital-key formula so
  issuing confers no private gain. These are combinable, and the euro uses all four.
- **The Eurozone's own history warns that centralized issuance alone was judged
  insufficient** — it needed fiscal-discipline rules layered on top, and even those
  failed on *enforcement* (Greece 2010; France/Germany 2003). If the sim adopts caps, the
  hard part is credible enforcement, not stating the rule.
- **Seigniorage capture is the mechanism to name explicitly.** Whoever mints profits
  while everyone shares dilution; that asymmetry is the engine of every failure mode
  here. A pooling/redistribution rule removes the private incentive to over-issue and
  reframes issuance as collective policy.
- **A federated issuer with distributed governance but centralized issuance decisions
  (the Eurosystem model) is the historically attested middle path** between "one
  omnipotent sovereign mint" and "everyone prints freely" — worth holding open as a
  design option that preserves polity agency without preserving the dilution race.

---

## Sources

- [EMU stability rests too much on the ECB (KBC)](https://www.kbc.com/en/economics/publications/emu-stability-rests-too-much-on-ecb.html)
- [Economic and monetary union (Grokipedia)](https://grokipedia.com/page/Economic_and_monetary_union)
- [From 'No Bailout' to the European Stability Mechanism (Fordham Int'l Law Journal)](https://ir.lawnet.fordham.edu/cgi/viewcontent.cgi?article=2456&context=ilj)
- [Safeguarding the euro as a currency beyond the state (ECB Occasional Paper 173)](https://www.ecb.europa.eu/pub/pdf/scpops/ecbop173.en.pdf)
- [Interpreting TARGET2 balances (BIS Working Paper 393)](https://www.bis.org/publ/work393.pdf)
- [TARGET2: Symptom, Not Cause, of Eurozone Woes (Richmond Fed)](https://www.richmondfed.org/publications/research/economic_brief/2012/eb_12-08)
- [Latin Monetary Union (Wikipedia)](https://en.wikipedia.org/wiki/Latin_Monetary_Union)
- [The Latin Monetary Union: evidence on Europe's failed common currency (ScienceDirect)](https://www.sciencedirect.com/science/article/pii/S1879933711000029)
- [Chronic Sovereign Debt Crises in the Eurozone, 2010–2012 (Minneapolis Fed)](https://www.minneapolisfed.org/article/2012/chronic-sovereign-debt-crises-in-the-eurozone-20102012)
- [The Analytics of the Greek Crisis (NBER)](https://pages.stern.nyu.edu/~tphilipp/papers/Greece.pdf)
- [Optimum currency area (Wikipedia)](https://en.wikipedia.org/wiki/Optimum_currency_area)
- [Risk Sharing and the Theory of Optimal Currency Areas (AOF working paper)](https://www.aof.org.hk/uploads/publication/327/ub_full_0_2_66_wp8.pdf)
- [Eurowinners and Eurolosers: The distribution of seigniorage wealth in EMU (CESifo/ScienceDirect)](https://ideas.repec.org/p/ces/ceswps/_134.html)
- [FAQs on the ECB's Annual Accounts (ECB)](https://www.ecb.europa.eu/press/publications/html/ecb.faq_ECB_Annual_Accounts.en.html)
- [ECB adjusts its capital key (ECB press release, 2023)](https://www.ecb.europa.eu/press/pr/date/2023/html/ecb.pr231221~173a7ba501.en.html)
- [What is seigniorage? (ECB explainer)](https://www.ecb.europa.eu/ecb-and-you/explainers/tell-me/html/seigniorage.en.html)
