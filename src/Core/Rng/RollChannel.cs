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
}
