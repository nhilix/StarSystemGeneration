namespace StarGen.Core.Rng;

/// <summary>
/// Channel registry (spec §8). Values are STABLE: never renumber or reuse a
/// shipped value. New rolls append new values; removed rolls retire theirs.
/// </summary>
public enum RollChannel : ulong
{
    Presence = 1,
    StarArrangement = 2,
    StarType = 3,          // subIndex = star index (0 primary, 1..2 companions)
    StarAge = 4,
    SlotCount = 5,
    CompanionSlot = 6,     // subIndex = companion index
    BodyKind = 7,          // index = starIndex*100 + slotIndex (BodyGenerator); subIndex unused (always 0)
    BodySize = 8,
    Atmosphere = 9,
    Hydrographics = 10,
    Biosphere = 11,
    Settlement = 12,
    PopulationTier = 13,
    Government = 14,
    OrderTier = 15,
    PortTier = 16,
    SatelliteCount = 17,   // index = starIndex*100 + slotIndex (parent body); subIndex unused (always 0)
    SatelliteKind = 18,    // index = starIndex*100 + slotIndex (parent body); subIndex = satellite index
    SatelliteSize = 19,    // index = starIndex*100 + slotIndex (parent body); subIndex = satellite index
    NameLength = 20,
    NameSyllable = 21,     // index = name slot, subIndex = syllable position
    OverlayChance = 22,
    OverlayPick = 23,

    // --- Regional generation (slice 1). Values stable per registry discipline. ---
    NoiseDensityLattice = 24,  // lattice draws: index = lattice x, subIndex = lattice y (via ValueNoise packing)
    NoiseWarpLattice = 25,
    NoiseStellarLattice = 26,
    NoiseMetalLattice = 27,
    AnchorPlacement = 28,      // cell-keyed: RollContext coordinate = cell coords
    AnchorKind = 29,
    HomeworldPlacement = 30,
    SpeciesEmbodiment = 31,
    SpeciesTemperament = 32,   // subIndex = temperament axis ordinal
    SimExpansion = 33,         // reserved - stage-1 expansion is roll-free; value must not be reused
    SimDevelopment = 34,       // retired with the stage-1 coin-flip loop - value must not be reused
    SimWar = 35,               // war-declaration gate: index = epoch, subIndex = polity id
    SimBattle = 36,            // front-cell contest: index = epoch, subIndex = war id (cell-keyed context)

    // --- Epoch frame (slice A). Stub channels retire with Slice B/F seeding;
    // values must not be reused. Keyed (step, actor id) per EpochRolls. ---
    EpochEmergenceEntry = 37,  // stub emergence schedule: actor = polity id
    EpochStubSeat = 38,        // stub homeworld seat: actor = polity id, subIndex = axis (0 q, 1 r)
    EpochStubName = 39,        // stub polity name: actor = polity id, subIndex = syllable (0 = length)

    // --- Epoch frame (slice B). ---
    EpochEntrySchedule = 40,   // stub emergence schedule until slice F: actor = polity id
}
