using System;
using System.Collections.Generic;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>Seats and recomputes the polity interior
/// (polity/factions-and-government.md §Legitimacy and cohesion). Pure state
/// mechanics — the Interior phase calls these; no rolls, no events yet.</summary>
public static class InteriorOps
{
    /// <summary>Seat a polity's interior at entry: the species ideology tilt
    /// becomes both the popular and the official line (they agree at birth),
    /// and the tilt picks the government form.</summary>
    public static void SeatAtEntry(SimState state, PolityRecord pr)
    {
        var species = state.Skeleton.Species[pr.SpeciesId];
        var tilt = GovernmentForms.SpeciesIdeologyTilt(species);
        var interior = new PolityInterior
        {
            FormId = GovernmentForms.SeatFor(species, tilt),
            // one culture per species until the schism mechanic splits them
            FoundingCultureId = pr.SpeciesId,
        };
        for (int ax = 0; ax < 4; ax++) interior.OfficialIdeology[ax] = tilt[ax];
        pr.Interior = interior;
    }

    /// <summary>Recompute every entered polity's interior scalars from real
    /// state. Returns how many were recomputed (the phase note).</summary>
    public static int Recompute(SimState state)
    {
        var knobs = state.Config.Interior;
        int years = state.Config.Sim.YearsPerEpoch;
        int recomputed = 0;
        var ownPorts = new List<Port>();
        foreach (var pr in state.Polities)                    // actor-id order
        {
            var interior = pr.Interior;
            if (interior == null || !state.Actors[pr.ActorId].Entered) continue;
            ownPorts.Clear();
            foreach (var p in state.Ports)
                if (p.OwnerActorId == pr.ActorId) ownPorts.Add(p);
            if (ownPorts.Count == 0) continue;
            recomputed++;

            // the popular line, mean SoL, and the culture mix — one segment sweep
            Span<double> popular = stackalloc double[4];
            double sizeSum = 0, solSum = 0, minoritySize = 0;
            var cultures = new HashSet<int>();
            foreach (var s in state.Segments)
            {
                if (s.Size <= 0
                    || state.Ports[s.PortId].OwnerActorId != pr.ActorId) continue;
                sizeSum += s.Size;
                solSum += s.SoL * s.Size;
                cultures.Add(s.CultureId);
                if (s.CultureId != interior.FoundingCultureId) minoritySize += s.Size;
                for (int ax = 0; ax < 4; ax++)
                    popular[ax] += s.Ideology[ax] * s.Size;
            }
            if (sizeSum <= 0) continue;
            double meanSoL = solSum / sizeSum;
            for (int ax = 0; ax < 4; ax++) popular[ax] /= sizeSum;

            // official chases popular at the form's inertia
            var form = GovernmentForms.Get(interior.FormId);
            double drift = Math.Min(1.0,
                (1.0 - form.PolicyInertia) * knobs.OfficialDriftPerYear * years);
            double gap = 0;
            for (int ax = 0; ax < 4; ax++)
            {
                interior.OfficialIdeology[ax] +=
                    (popular[ax] - interior.OfficialIdeology[ax]) * drift;
                gap += Math.Abs(popular[ax] - interior.OfficialIdeology[ax]);
            }
            gap /= 4;

            // legitimacy: form-weighted blend of the five terms
            double prosperity = Clamp01(
                meanSoL + knobs.SoLTrendGain * (meanSoL - interior.LastMeanSoL));
            double alignment = Clamp01(1.0 - 2.0 * gap);
            double ruler = RulerPrestige(state, interior);
            // war outcomes: winning steadies a throne, a grinding or
            // losing war saps it — weariness is the interior responding
            double war = WarResolution.WarScore(state, pr.ActorId);
            double accommodation = Clamp01(1.0 - minoritySize / sizeSum);
            double wP = knobs.LegitimacyProsperityWeight * form.LegitimacyProsperityWeight;
            double wI = knobs.LegitimacyIdeologyWeight * form.LegitimacyIdeologyWeight;
            double wR = knobs.LegitimacyRulerWeight * form.LegitimacyRulerWeight;
            double wW = knobs.LegitimacyWarWeight;
            double wA = knobs.LegitimacyAccommodationWeight;
            interior.Legitimacy = Clamp01(
                (wP * prosperity + wI * alignment + wR * ruler + wW * war
                 + wA * accommodation) / (wP + wI + wR + wW + wA));
            interior.LastMeanSoL = meanSoL;

            // cohesion: legitimacy discounted by structural strain
            int meanDist = 0;
            var seat = state.Actors[pr.ActorId].Seat;
            foreach (var p in ownPorts) meanDist += HexGrid.Distance(seat, p.Hex);
            double distStrain = ownPorts.Count == 0 ? 0
                : (double)meanDist / ownPorts.Count
                  / Math.Max(1, state.Config.Expansion.ColonizationReachHexes);
            double strain = knobs.StrainPerPort * (ownPorts.Count - 1)
                            + knobs.StrainPerCulture * (cultures.Count - 1)
                            + knobs.StrainDistanceWeight * distStrain;
            interior.Cohesion = Math.Max(form.CohesionFloor,
                Clamp01(interior.Legitimacy - strain));

            // enforcement: warship hulls stationed per port
            int warships = 0;
            foreach (var fleet in state.Fleets)
            {
                if (fleet.OwnerActorId != pr.ActorId) continue;
                foreach (var g in fleet.Hulls)
                {
                    var role = state.Designs[g.DesignId].Role;
                    if (role == ShipRole.Escort || role == ShipRole.Line)
                        warships += g.Count;
                }
            }
            interior.Enforcement = Clamp01(knobs.EnforcementBase
                + knobs.EnforcementPerWarshipPerPort * warships / ownPorts.Count);
        }
        return recomputed;
    }

    /// <summary>The ruler-prestige legitimacy term: neutral 0.5 plus the
    /// occupant's renown and house prestige (characters.md §Renown).</summary>
    private static double RulerPrestige(SimState state, PolityInterior interior)
    {
        int id = interior.RulerCharacterId;
        if (id < 0 || id >= state.Characters.Count) return 0.5;
        var ruler = state.Characters[id];
        double house = ruler.DynastyId >= 0
            ? state.Dynasties[ruler.DynastyId].Prestige : 0.0;
        return Clamp01(0.5 + state.Config.Character.PrestigePerRenown
                             * (ruler.Renown + house));
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
