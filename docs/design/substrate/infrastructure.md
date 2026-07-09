# Infrastructure Vocabulary

*Status: awaiting design pass (1). This stub states scope only.*

The catalog of buildable assets: what kinds of facility exist, where they can and
want to be built, and what they mechanically do.

Defines, when designed:

- The facility catalog: mines, shipyards, spaceports, depots, stations, fortresses,
  and the rest — a closed, versioned vocabulary like the anchor types.
- Siting rules per type (mines want belts, spaceports want route junctions) and
  scale tiers.
- Mechanical effects on the containing cell and the owning institution: extraction
  multipliers, ship production capacity, trade cost/efficiency, defense.
- Hex-tier anchoring: how built infrastructure becomes pre-commitments so the
  facility a player visits is the one the simulation built.

Ownership, investment, and lifecycle (construction, condition, destruction,
nationalization) belong to [../economy/](../economy/).
