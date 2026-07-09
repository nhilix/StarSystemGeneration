using System;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Model;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Slice-A placeholder genesis: seeds stub polity actors with rolled
/// names, seat hexes, and staggered entry epochs, so the seven-phase frame has
/// something to step. Slice B replaces this with the real state model; Slice F
/// replaces the schedule with simulated emergence.</summary>
public static class StubGenesis
{
    public static SimState Seed(EpochSimConfig config)
    {
        var state = new SimState(config);
        int windowEpochs = Math.Max(1, config.Genesis.EmergenceWindowYears
                                       / config.Sim.YearsPerEpoch);
        for (int id = 0; id < config.Genesis.StubPolityCount; id++)
        {
            int entry = EpochRolls.NextInt(config.MasterSeed, RollChannel.EpochEmergenceEntry,
                                           step: 0, actorId: id, 0, windowEpochs + 1);
            int radius = config.Genesis.StubSeatRadiusHexes;
            var seat = new HexCoordinate(
                EpochRolls.NextInt(config.MasterSeed, RollChannel.EpochStubSeat,
                                   step: 0, actorId: id, -radius, radius + 1, subIndex: 0),
                EpochRolls.NextInt(config.MasterSeed, RollChannel.EpochStubSeat,
                                   step: 0, actorId: id, -radius, radius + 1, subIndex: 1));
            state.Actors.Add(new Actor(id, ActorKind.Polity, RollName(config, id),
                                       seat, entry, new TrivialController()));
        }
        return state;
    }

    private static string RollName(EpochSimConfig config, int id)
    {
        int syllables = EpochRolls.NextInt(config.MasterSeed, RollChannel.EpochStubName,
                                           step: 0, actorId: id, 2, 4);
        string name = "";
        for (int i = 0; i < syllables; i++)
            name += NameTables.Syllables.Pick(
                EpochRolls.NextDouble(config.MasterSeed, RollChannel.EpochStubName,
                                      step: 0, actorId: id, subIndex: i + 1));
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
    }
}
