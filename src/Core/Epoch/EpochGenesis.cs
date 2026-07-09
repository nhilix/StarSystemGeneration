using System;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Seeds the epoch sim from the natural raster: one polity actor per
/// homeworld anchor the seeding passes placed (SkeletonBuilder.BuildNatural),
/// in cell spiral order — id = ordinal, name = species name, seat = anchor
/// hex. Entry epochs are a rolled stub schedule (EpochEntrySchedule) until
/// slice F simulates emergence. Homeworlds are simply the first ports: the
/// port itself is founded at entry, by the Interior phase.</summary>
public static class EpochGenesis
{
    public static SimState Seed(GalaxySkeleton skeleton, EpochSimConfig config)
    {
        var state = new SimState(config, skeleton);
        int windowEpochs = Math.Max(1, config.Genesis.EmergenceWindowYears
                                       / config.Sim.YearsPerEpoch);
        foreach (var cell in skeleton.Cells)                 // spiral order (P6)
            foreach (var anchor in cell.Anchors)
            {
                if (anchor.Type != AnchorType.Homeworld) continue;
                int id = state.Actors.Count;
                int entry = EpochRolls.NextInt(config.MasterSeed,
                    RollChannel.EpochEntrySchedule, step: 0, actorId: id,
                    0, windowEpochs + 1);
                state.Actors.Add(new Actor(id, ActorKind.Polity,
                    skeleton.Species[anchor.SpeciesId].Name, anchor.Hex, entry,
                    new GenesisController(config)));
                state.Polities.Add(new PolityRecord(id, anchor.SpeciesId));
            }
        return state;
    }
}
