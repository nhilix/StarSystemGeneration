using System;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Plagues (slice I): outbreaks roll where people crowd, spread
/// rides the posted-traffic lanes (quarantines and blockades stop contagion
/// as surely as freight), mortality shrinks segments — never minting or
/// destroying wealth (the dead leave inheritances) — Life tech blunts the
/// toll, machine minds don't sicken, and every infection burns out into an
/// immunity window. Runs in Interior before demographics.</summary>
public static class PlagueOps
{
    private static readonly string[] Strains =
        { "Fever", "Rot", "Blight", "Pox", "Wasting" };

    /// <summary>One epoch of contagion. Returns (outbreaks, ports newly
    /// infected, plagues burned out) for the phase note.</summary>
    public static (int Outbreaks, int Spread, int BurnedOut) Step(SimState state)
    {
        var knobs = state.Config.Plague;
        int years = state.Config.Sim.YearsPerEpoch;
        int outbreaks = Outbreaks(state, knobs, years);
        int spread = 0, burnedOut = 0;
        var severed = FleetOps.SeveredLaneIds(state);
        foreach (var plague in state.Plagues)             // id order (P6)
        {
            if (!plague.Active) continue;
            spread += Spread(state, plague, knobs, years, severed);
            Mortality(state, plague, knobs, years);
            if (Burnout(state, plague, knobs, years)) burnedOut++;
        }
        return (outbreaks, spread, burnedOut);
    }

    /// <summary>Index cases: crowded ports roll the outbreak gate; machine
    /// populations host nothing. One strain per port at a time.</summary>
    private static int Outbreaks(SimState state, PlagueKnobs knobs, int years)
    {
        int outbreaks = 0;
        foreach (var port in state.Ports)                 // id order (P6)
        {
            double organic = OrganicPopulation(state, port.Id);
            if (organic <= 0.5 || Afflicted(state, port.Id)) continue;
            double cap = Math.Max(1.0, port.Tier
                * state.Config.Expansion.SegmentCapPerTier);
            double crowding = Math.Min(1.0, organic / cap);
            double chance = knobs.OutbreakChancePerYear * years * crowding;
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    RollChannel.PlagueOutbreak, state.EpochIndex, port.Id, 0)
                >= chance) continue;
            var plague = new Plague(state.Plagues.Count,
                PlagueName(state, state.Plagues.Count), port.Id,
                state.WorldYear);
            plague.InfectedSince.Add(port.Id, state.WorldYear);
            state.Plagues.Add(plague);
            outbreaks++;
            state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                WorldEventType.PlagueOutbreak,
                new[] { port.OwnerActorId }, port.Hex, Magnitude: 2.0,
                Valence: -0.8, EventVisibility.Public,
                new PlagueOutbreakPayload(plague.Id, plague.Name, port.Id)));
        }
        return outbreaks;
    }

    /// <summary>Contagion rides the lanes at traffic-gated odds — busy
    /// corridors carry plague exactly as they carry news; severed and
    /// quarantined lanes carry neither.</summary>
    private static int Spread(SimState state, Plague plague, PlagueKnobs knobs,
                              int years, System.Collections.Generic.HashSet<int> severed)
    {
        int spread = 0;
        foreach (var lane in state.Lanes)                 // id order (P6)
        {
            if (severed.Contains(lane.Id)) continue;
            int from, to;
            if (plague.Infects(lane.PortAId) && !Afflicted(state, lane.PortBId))
            { from = lane.PortAId; to = lane.PortBId; }
            else if (plague.Infects(lane.PortBId)
                     && !Afflicted(state, lane.PortAId))
            { from = lane.PortBId; to = lane.PortAId; }
            else continue;
            if (plague.ImmuneUntil.TryGetValue(to, out long lapse)
                && lapse >= state.WorldYear) continue;
            if (OrganicPopulation(state, to) <= 0.5) continue;
            double traffic = FleetOps.TrafficPerYear(state, lane);
            double saturation = knobs.SpreadTrafficSaturation <= 0 ? 1.0
                : Math.Min(1.0, traffic / knobs.SpreadTrafficSaturation);
            double chance = knobs.SpreadChancePerYear * years * saturation;
            if (EpochRolls.NextDouble(state.Config.MasterSeed,
                    RollChannel.PlagueSpread, state.EpochIndex, plague.Id,
                    lane.Id) >= chance) continue;
            plague.InfectedSince.Add(to, state.WorldYear);
            spread++;
        }
        return spread;
    }

    /// <summary>The toll: infected ports' organic segments shrink; wealth
    /// stays with the segment (the dead leave inheritances — deaths never
    /// mint or burn a credit, P4). Life tech blunts the rate.</summary>
    private static void Mortality(SimState state, Plague plague,
                                  PlagueKnobs knobs, int years)
    {
        for (int i = 0; i < plague.InfectedSince.Count; i++)
        {
            int portId = plague.InfectedSince.Keys[i];
            int owner = state.Ports[portId].OwnerActorId;
            int lifeTier = state.PolityOf(owner).TechTier[(int)TechDomain.Life];
            double rate = knobs.MortalityPerYear * years
                * Math.Max(0.0, 1.0 - knobs.MortalityLifeTierDiscount * lifeTier);
            if (rate <= 0) continue;
            foreach (var seg in state.Segments)           // id order (P6)
            {
                if (seg.PortId != portId || seg.Size <= 0) continue;
                if (MarketEngine.EmbodimentOf(state, seg.SpeciesId)
                    == Galaxy.Embodiment.Machine) continue;
                double deaths = seg.Size * Math.Min(0.9, rate);
                seg.Size -= deaths;
                plague.TotalDeaths += deaths;
            }
        }
    }

    /// <summary>Infections clear after the burnout window into an immunity
    /// window; a plague with nowhere left to burn is history. Plagues burn
    /// out — they never sterilize the galaxy.</summary>
    private static bool Burnout(SimState state, Plague plague,
                                PlagueKnobs knobs, int years)
    {
        for (int i = plague.InfectedSince.Count - 1; i >= 0; i--)
        {
            if (state.WorldYear - plague.InfectedSince.Values[i]
                < knobs.BurnoutYears) continue;
            int portId = plague.InfectedSince.Keys[i];
            plague.InfectedSince.RemoveAt(i);
            plague.ImmuneUntil[portId] = state.WorldYear
                + (long)knobs.ImmunityYears;
        }
        if (plague.InfectedSince.Count > 0) return false;
        plague.Active = false;
        plague.EndedYear = state.WorldYear;
        var origin = state.Ports[plague.OriginPortId];
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.PlagueBurnedOut, new[] { origin.OwnerActorId },
            origin.Hex, Magnitude: Math.Max(1.0, plague.TotalDeaths),
            Valence: 0.4, EventVisibility.Public,
            new PlagueBurnedOutPayload(plague.Id, plague.Name,
                                       plague.TotalDeaths)));
        return true;
    }

    /// <summary>Any active strain at the port (one at a time — a sick city
    /// doesn't catch a second cold).</summary>
    public static bool Afflicted(SimState state, int portId)
    {
        foreach (var plague in state.Plagues)
            if (plague.Active && plague.Infects(portId)) return true;
        return false;
    }

    /// <summary>Resolve a QuarantineAct: the sovereign of either end may
    /// close a lane (Resolution — the typed act finally learns its
    /// consequence). Freight, migration, and contagion stop together.</summary>
    public static bool Quarantine(SimState state, QuarantineAct act)
    {
        if (act.LaneId < 0 || act.LaneId >= state.Lanes.Count) return false;
        var lane = state.Lanes[act.LaneId];
        int ownerA = state.Ports[lane.PortAId].OwnerActorId;
        int ownerB = state.Ports[lane.PortBId].OwnerActorId;
        if (act.ActorId != ownerA && act.ActorId != ownerB) return false;
        long until = state.WorldYear
            + (long)state.Config.Plague.QuarantineYears;
        if (lane.QuarantinedUntil >= until) return false;   // already held
        lane.QuarantinedUntil = until;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.QuarantineImposed, new[] { act.ActorId },
            state.Ports[lane.PortAId].Hex, Magnitude: 1.0, Valence: -0.3,
            EventVisibility.Regional,
            new QuarantineImposedPayload(act.ActorId, lane.Id)));
        return true;
    }

    private static string PlagueName(SimState state, int plagueId)
    {
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed,
            RollChannel.PlagueOutbreak, 0, plagueId, 100) < 0.4 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.PlagueOutbreak, 0, plagueId, i + 1));
        string strain = Strains[EpochRolls.NextInt(seed,
            RollChannel.PlagueOutbreak, 0, plagueId, 0, Strains.Length, 200)];
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word)
               + " " + strain;
    }

    private static double OrganicPopulation(SimState state, int portId)
    {
        double total = 0;
        foreach (var seg in state.Segments)
            if (seg.PortId == portId
                && MarketEngine.EmbodimentOf(state, seg.SpeciesId)
                   != Galaxy.Embodiment.Machine)
                total += seg.Size;
        return total;
    }
}
