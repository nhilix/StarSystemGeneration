# The Controller Contract

The canonical enumeration of the Intent-phase interface (P2):
`Decide(perceivedState) → (policies, acts)` per actor kind. This is
simultaneously the AI's API and the player UI surface at each scope. Subsystem
docs define the mechanics behind each entry; this appendix is the authoritative
list.

## Polity

**Standing policies** (applied mechanically by other phases):

| Policy | Consumed by |
|---|---|
| budget weights: development · military · research (per tech domain) · expansion · appeasement · reserves | Allocation |
| standing plan: a prioritized schedule of projects (sited facility, port raise, gate pair, hull batch, mobilization) with target start years, packed against perceived free capability | Allocation → groundbreaking |
| tax rate, tariff schedule (per polity/good) | Markets |
| law code: legality per good (legal/restricted/prohibited) | Markets, patrol enforcement |
| charter openness | corporate founding |
| military doctrine (posture and engagement biases) | Resolution |
| shipbuilding priorities (design mix, refit variants) | Allocation → yards |
| stockpile targets (per good) | Allocation → depots/reserves |
| diplomatic posture per known polity | Intent AI itself, treaty seeking |
| native policy (protectorate / integrate / exploit / uplift) | contact resolution |

**Discrete acts** (resolved the same step):

found colony (target + convoy) · declare war (casus belli + objectives + demand) ·
offer/accept/break treaty rung · sanction · settlement accept/reject ·
nationalize · grant/revoke charter · dynastic marriage/wardship · vassalage
offer/demand · quarantine (self-imposed lane closure) · post procurement contract
(good, destination, premium — escrowed).

## Corporation

**Policies**: investment allocation (facilities, fleet, depots) · standing plan
(a scheduled portfolio of investment projects packed against perceived free
capability) · route bids · dividend rate · lobby targets · risk appetite
(legality margin — how far into black books it operates).

**Acts**: charter application · major acquisition or abandonment · headquarters
relocation · post procurement contract.

## Character (role-scoped + personal)

Role-holders act *through* their institution's controller (their personality
already colors it — the temperament composition). **Personal acts**: patronize a
faction · defect · accept/refuse a role · marry (dynastic) · lead an expedition.

## Not controllers

Factions exert pressure mechanically (no slot until graduation); populations
respond statistically; assets are acted through. Fleets execute postures assigned
by their owner's policies/acts; commanders bias execution, they do not hold the
slot.

## Contract rules

- New mechanics may **extend** these lists (additions are frame-safe); they may
  not add decision points outside the Intent phase.
- Every policy and act must be expressible from *perceived* state only (P3).
- A player occupying a slot sees exactly this list at that scope — nothing the AI
  can do is hidden from the player, and vice versa (P2).
