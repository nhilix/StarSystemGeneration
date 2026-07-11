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
    AnchorKind = 29,           // mineral roll (0) + site-anchor sampling gate (1) since slice F
    HomeworldPlacement = 30,   // retired (slice F: homeworlds derive from origins) - value must not be reused
    SpeciesEmbodiment = 31,
    SpeciesTemperament = 32,   // subIndex = temperament axis ordinal
    SimExpansion = 33,         // reserved - stage-1 expansion is roll-free; value must not be reused
    SimDevelopment = 34,       // retired with the stage-1 coin-flip loop - value must not be reused
    SimWar = 35,               // war-declaration gate: index = epoch, subIndex = polity id
    SimBattle = 36,            // front-cell contest: index = epoch, subIndex = war id (cell-keyed context)

    // --- Epoch frame (slice A). Retired with slice B's EpochGenesis;
    // values must not be reused. ---
    EpochEmergenceEntry = 37,  // retired (slice B) - value must not be reused
    EpochStubSeat = 38,        // retired (slice B) - value must not be reused
    EpochStubName = 39,        // retired (slice B) - value must not be reused

    // --- Epoch frame (slice B). ---
    EpochEntrySchedule = 40,   // retired (slice F: entry derives from the emergence schedule) - value must not be reused

    // --- Cosmic clock (slice F). Rolls keyed (step, cell spiral index). ---
    CosmicInflowClump = 41,    // inflow clumping noise per (step, cell)
    CosmicSfTrigger = 42,      // star-formation trigger noise per (step, cell)
    CosmicMergerSchedule = 43, // merger count/bearing/epoch/mass: actor = merger index, subIndex = field
    CosmicGlobularPlace = 44,  // globular count/cell picks: actor = globular index
    CosmicAgnTrigger = 45,     // accretion-epoch gate: step-keyed
    CosmicFeatureName = 46,    // feature name syllables: actor = feature ordinal, subIndex = syllable

    // --- Evolutionary clock (slice F). Rolls keyed (step, cell spiral index). ---
    EvoAbiogenesis = 47,       // life-starts gate per (step, cell)
    EvoCatastrophe = 48,       // mass-extinction gate per (step, cell); subIndex 1 = full-reset roll
    EvoSpread = 49,            // panspermia gate per (step, target cell); subIndex = neighbor ordinal
    EvoSapience = 50,          // sapience-registration gate per (step, cell); subIndex 1 = homeworld hex pick
    EvoMaturation = 51,        // maturation-duration variance per origin (actor = origin id)

    // --- Precursor arcs (slice F). Rolls keyed (step, wave id) unless noted. ---
    WaveVigor = 52,            // class + vigor draws: actor = wave id, subIndex = field
    WaveExpand = 53,           // expansion picks: subIndex = claim ordinal
    WaveEnd = 54,              // end-cause draw + decline variance
    WaveContact = 55,          // inter-wave contact resolution
    WaveEngineer = 56,         // biosphere-engineering gate: actor = cell, subIndex = wave
    WaveDormant = 57,          // dormant-survival roll per site: actor = wave, subIndex = site ordinal
    WaveName = 58,             // wave name syllables: actor = wave id, subIndex = syllable
    WaveDescendant = 59,       // machine-descendant gate + entry-date roll

    // --- Characters (slice G). ---
    CharacterName = 60,        // name syllables: step = culture id, actor = character id, subIndex = syllable (100 = length)
    CharacterTraits = 61,      // personality: step = mint epoch, actor = character id, subIndex = trait ordinal
    CharacterDeath = 62,       // per-epoch death check: step = epoch, actor = character id

    // --- Factions (slice G). ---
    FactionSeed = 63,          // faction name syllables: step = polity id, actor = faction id, subIndex = syllable (100 = length)
    Graduation = 64,           // success roll (step = epoch, actor = faction id); subIndex 1 = contested-coup roll; schism culture names key (step = new actor id, actor = -1)

    // --- Corporations (slice G). ---
    CorpSeed = 65,             // corp name syllables: actor = corp actor id, subIndex = syllable (100 = length)

    // --- Relations & war (slice H). ---
    FederationSeed = 66,       // federation name syllables: step = new actor id, actor = -1, subIndex = syllable (100 = length)
    WarSpark = 67,             // border-incident roll: step = epoch, actor = pair's lower polity id, subIndex = higher
    Battle = 68,               // engagement resolution: step = epoch, actor = war id, subIndex = objective id
    CommanderFate = 69,        // commander death on a decisive defeat: step = epoch, actor = commander character id

    // --- Narrative (slice I). ---
    PlagueOutbreak = 70,       // outbreak gate: step = epoch, actor = port id; subIndex 1+ = name syllables (100 = length, 200 = strain)
    PlagueSpread = 71,         // lane-borne spread: step = epoch, actor = plague id, subIndex = lane id
}
