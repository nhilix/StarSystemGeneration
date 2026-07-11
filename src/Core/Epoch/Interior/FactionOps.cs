using System;
using System.Collections.Generic;
using System.Globalization;
using StarGen.Core.Content;
using StarGen.Core.Galaxy;
using StarGen.Core.Rng;

namespace StarGen.Core.Epoch;

/// <summary>Faction formation, strength, pressure, appeasement, and
/// grievance (polity/factions-and-government.md). Runs inside the Interior
/// phase; the appeasement budget line pays out in Allocation. Graduation
/// (task 5) reads the state this leaves behind.</summary>
public static class FactionOps
{
    /// <summary>The chronicle suffix per basis — the name's second word.</summary>
    private static readonly string[] Suffix =
        { "Front", "Voice", "League", "Combine", "Guard", "Creed" };

    /// <summary>Per-basis budget emphasis (BudgetWeights order: development,
    /// military, research, expansion, appeasement, reserves). Null where the
    /// agenda is ideological, not fiscal. Catalog data.</summary>
    private static readonly double[]?[] BasisBudget =
    {
        null,                                         // ideological
        new[] { 0.30, 0.10, 0.10, 0.10, 0.30, 0.10 }, // cultural: care + buy-off
        new[] { 0.35, 0.10, 0.05, 0.30, 0.15, 0.05 }, // regional: invest the frontier
        new[] { 0.30, 0.10, 0.25, 0.15, 0.10, 0.10 }, // corporate: growth + research
        new[] { 0.15, 0.45, 0.15, 0.10, 0.05, 0.10 }, // military: the sword
        new[] { 0.25, 0.10, 0.05, 0.10, 0.40, 0.10 }, // sacral: tithes and alms
    };

    /// <summary>One epoch of internal politics for every entered polity:
    /// form where interests diverge, recompute strength/militancy, press the
    /// official ideology, settle grievance against this epoch's appeasement.
    /// Returns (formed, dissolved) for the phase note.</summary>
    public static (int Formed, int Dissolved) Step(SimState state)
    {
        int formed = 0, dissolved = 0;
        foreach (var pr in state.Polities)                    // actor-id order
        {
            if (pr.Interior == null || !state.Actors[pr.ActorId].Entered)
                continue;
            var scan = ScanPolity(state, pr);
            if (scan.PopTotal <= 0) continue;
            formed += Form(state, pr, scan);
            Update(state, pr, scan, ref dissolved);
        }
        // factions of depopulated or unentered polities never reach Update —
        // their epoch-transients still must not cross the epoch boundary
        foreach (var faction in state.Factions)
        {
            faction.PaidThisEpoch = 0;
            faction.DemandThisEpoch = 0;
        }
        return (formed, dissolved);
    }

    /// <summary>Blend a polity's declared budget toward its strong factions'
    /// agendas — bounded by strength and the form's tolerance (assemblies
    /// bend, autocracies resist and pay in grievance instead).</summary>
    public static BudgetWeights PressedBudget(SimState state, PolityRecord pr,
                                              BudgetWeights declared)
    {
        var interior = pr.Interior;
        if (interior == null) return declared;
        var knobs = state.Config.Faction;
        double tolerance = GovernmentForms.Get(interior.FormId).FactionTolerance;
        int years = state.Config.Sim.YearsPerEpoch;
        Span<double> w = stackalloc double[6]
        {
            declared.Development, declared.Military, declared.Research,
            declared.Expansion, declared.Appeasement, declared.Reserves,
        };
        double sum = 0;
        for (int i = 0; i < 6; i++) sum += w[i];
        foreach (var faction in state.Factions)               // id order (P6)
        {
            if (!faction.Active || faction.PolityId != pr.ActorId
                || faction.BudgetTarget == null) continue;
            double f = Math.Min(knobs.MaxBudgetPressure,
                knobs.PressureRatePerYear * years * faction.Strength * tolerance);
            for (int i = 0; i < 6; i++)
                w[i] += (faction.BudgetTarget[i] * sum - w[i]) * f;
        }
        // renormalize so pressure redirects spending, never mints any
        double pressed = 0;
        for (int i = 0; i < 6; i++) pressed += w[i];
        if (pressed > 0)
            for (int i = 0; i < 6; i++) w[i] *= sum / pressed;
        return new BudgetWeights(w[0], w[1], w[2], w[3], w[4], w[5]);
    }

    /// <summary>Allocation's appeasement line: each faction demands its
    /// strength's share of the same budget base the pool draws on (the
    /// economy sets the price of peace), is paid up to that demand, and is
    /// rationed pro-rata when the pool runs short — a polity whose factions
    /// outgrow its appeasement line accrues grievance it cannot pay away.
    /// Payouts cap at demand so war chests don't compound forever. Returns
    /// what was spent — a treasury→faction-wealth flow, conserved (P4).</summary>
    public static double SpendAppeasement(SimState state, PolityRecord pr,
                                          double pool, double allocatable)
    {
        double share = state.Config.Faction.AppeasementDemandShare;
        double totalDemand = 0;
        foreach (var faction in state.Factions)
            if (faction.Active && faction.PolityId == pr.ActorId)
            {
                faction.DemandThisEpoch =
                    faction.Strength * share * Math.Max(0.0, allocatable);
                totalDemand += faction.DemandThisEpoch;
            }
        if (totalDemand <= 0 || pool <= 0) return 0;
        double ration = Math.Min(1.0, pool / totalDemand);
        double spent = 0;
        foreach (var faction in state.Factions)               // id order (P6)
        {
            if (!faction.Active || faction.PolityId != pr.ActorId) continue;
            double pay = faction.DemandThisEpoch * ration;
            faction.Wealth += pay;
            faction.PaidThisEpoch += pay;
            spent += pay;
        }
        return spent;
    }

    // ---- formation ----

    /// <summary>One pass over a polity's segments: everything the six bases
    /// read (sizes, shares, ideology means, frontier, patron renown).</summary>
    private readonly struct PolityScan
    {
        public double PopTotal { get; init; }
        public double[] PopularIdeology { get; init; }
        public double MinorityShare { get; init; }
        public int LargestMinorityCulture { get; init; }
        public double FrontierShare { get; init; }
        public double DissenterShare { get; init; }
        public double[] DissenterIdeology { get; init; }
        public double SacralShare { get; init; }
        public double CommandRenown { get; init; }
    }

    private static PolityScan ScanPolity(SimState state, PolityRecord pr)
    {
        var knobs = state.Config.Faction;
        var interior = pr.Interior!;
        var seat = state.Actors[pr.ActorId].Seat;
        int frontierAt = (int)(knobs.FrontierDistanceFraction
            * state.Config.Expansion.ColonizationReachHexes);
        double popTotal = 0, minority = 0, frontier = 0, dissent = 0, sacral = 0;
        var popular = new double[4];
        var dissentMean = new double[4];
        var byCulture = new Dictionary<int, double>();
        foreach (var s in state.Segments)                     // id order (P6)
        {
            if (s.Size <= 0
                || state.Ports[s.PortId].OwnerActorId != pr.ActorId) continue;
            popTotal += s.Size;
            for (int ax = 0; ax < 4; ax++) popular[ax] += s.Ideology[ax] * s.Size;
            if (s.CultureId != interior.FoundingCultureId)
            {
                minority += s.Size;
                byCulture.TryGetValue(s.CultureId, out double held);
                byCulture[s.CultureId] = held + s.Size;
            }
            if (HexGrid.Distance(seat, state.Ports[s.PortId].Hex) > frontierAt)
                frontier += s.Size;
            double gap = 0;
            for (int ax = 0; ax < 4; ax++)
                gap += Math.Abs(s.Ideology[ax] - interior.OfficialIdeology[ax]);
            if (gap / 4 > knobs.IdeologyGapToForm)
            {
                dissent += s.Size;
                for (int ax = 0; ax < 4; ax++)
                    dissentMean[ax] += s.Ideology[ax] * s.Size;
            }
            if (s.Ideology[(int)IdeologyAxis.SacralMaterial] < knobs.SacralAxisLine)
                sacral += s.Size;
        }
        if (popTotal > 0)
            for (int ax = 0; ax < 4; ax++) popular[ax] /= popTotal;
        if (dissent > 0)
            for (int ax = 0; ax < 4; ax++) dissentMean[ax] /= dissent;
        int largestCulture = -1;
        double largestSize = 0;
        foreach (var kv in byCulture)
            if (kv.Value > largestSize
                || (kv.Value == largestSize && kv.Key < largestCulture))
            { largestSize = kv.Value; largestCulture = kv.Key; }
        double renown = 0;
        foreach (var c in state.Characters)
            if (c.Alive && c.PolityId == pr.ActorId
                && c.Role is CharacterRole.Marshal or CharacterRole.Commander)
                renown += c.Renown + 1;   // a standing officer corps counts
        return new PolityScan
        {
            PopTotal = popTotal,
            PopularIdeology = popular,
            MinorityShare = popTotal > 0 ? minority / popTotal : 0,
            LargestMinorityCulture = largestCulture,
            FrontierShare = popTotal > 0 ? frontier / popTotal : 0,
            DissenterShare = popTotal > 0 ? dissent / popTotal : 0,
            DissenterIdeology = dissentMean,
            SacralShare = popTotal > 0 ? sacral / popTotal : 0,
            CommandRenown = renown,
        };
    }

    private static int Form(SimState state, PolityRecord pr, PolityScan scan)
    {
        var knobs = state.Config.Faction;
        var interior = pr.Interior!;
        var species = state.Skeleton.Species[pr.SpeciesId];
        double tolerance = GovernmentForms.Get(interior.FormId).FactionTolerance;
        int formed = 0;

        bool Has(FactionBasis basis)
        {
            foreach (var f in state.Factions)
                if (f.Active && f.PolityId == pr.ActorId && f.Basis == basis)
                    return true;
            return false;
        }

        // hive unity and machine consensus barely factionalize: the form's
        // tolerance also gates *formation* below a floor share
        double minShare = knobs.FormMinShare * (1.0 + (1.0 - tolerance));

        if (scan.DissenterShare >= minShare && !Has(FactionBasis.Ideological))
        {
            var f = FoundFaction(state, pr, FactionBasis.Ideological);
            f.IdeologyTarget = (double[])scan.DissenterIdeology.Clone();
            formed++;
        }
        if (scan.LargestMinorityCulture >= 0 && !Has(FactionBasis.Cultural))
        {
            double share = 0;
            foreach (var s in state.Segments)
                if (s.Size > 0 && s.CultureId == scan.LargestMinorityCulture
                    && state.Ports[s.PortId].OwnerActorId == pr.ActorId)
                    share += s.Size;
            if (share / scan.PopTotal >= minShare)
            {
                var f = FoundFaction(state, pr, FactionBasis.Cultural);
                f.ContextId = scan.LargestMinorityCulture;
                f.BudgetTarget = BasisBudget[(int)FactionBasis.Cultural];
                formed++;
            }
        }
        if (scan.FrontierShare >= minShare && !Has(FactionBasis.Regional))
        {
            var f = FoundFaction(state, pr, FactionBasis.Regional);
            f.BudgetTarget = BasisBudget[(int)FactionBasis.Regional];
            formed++;
        }
        // corporate: dividends arm this basis in task 7
        if (scan.CommandRenown * species.Militancy >= knobs.MilitaryRenownToForm
            && !Has(FactionBasis.Military))
        {
            var f = FoundFaction(state, pr, FactionBasis.Military);
            f.BudgetTarget = BasisBudget[(int)FactionBasis.Military];
            formed++;
        }
        if (scan.SacralShare >= minShare
            && interior.OfficialIdeology[(int)IdeologyAxis.SacralMaterial]
               - knobs.SacralAxisLine > 0.1
            && !Has(FactionBasis.Sacral))
        {
            var f = FoundFaction(state, pr, FactionBasis.Sacral);
            var target = (double[])interior.OfficialIdeology.Clone();
            target[(int)IdeologyAxis.SacralMaterial] = 0.0;
            f.IdeologyTarget = target;
            f.BudgetTarget = BasisBudget[(int)FactionBasis.Sacral];
            // a prophet seeds the faction that follows (characters.md) —
            // notables stay capped even when a role-holder earns the name
            var leader = state.Characters[f.LeaderCharacterId];
            if (CharacterOps.NotableCount(state, pr.ActorId)
                < state.Config.Character.MaxNotablesPerPolity)
            {
                leader.Notable = NotableType.Prophet;
                state.Staged.Add(new StagedEvent(ClockStratum.Generational,
                    WorldEventType.NotableEmerged, new[] { pr.ActorId },
                    state.Actors[pr.ActorId].Seat, Magnitude: 1.0, Valence: 0.0,
                    EventVisibility.Regional,
                    new NotableEmergedPayload(leader.Id, leader.Name,
                                              (int)NotableType.Prophet)));
            }
            formed++;
        }
        return formed;
    }

    /// <summary>Register a faction with a syllable-flavored name and a
    /// freshly minted leader; chronicle the coalescence. Public since task
    /// 7: the niche watcher raises merchant factions through it.</summary>
    public static Faction FoundFaction(SimState state, PolityRecord pr,
                                       FactionBasis basis)
    {
        int id = state.Factions.Count;
        ulong seed = state.Config.MasterSeed;
        int syllables = 2 + (EpochRolls.NextDouble(seed,
            RollChannel.FactionSeed, pr.ActorId, id, 100) < 0.3 ? 1 : 0);
        string word = "";
        for (int i = 0; i < syllables; i++)
            word += NameTables.Syllables.Pick(EpochRolls.NextDouble(seed,
                RollChannel.FactionSeed, pr.ActorId, id, i));
        string name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word)
                      + " " + Suffix[(int)basis];
        var faction = new Faction(id, name, pr.ActorId, basis, state.WorldYear);
        state.Factions.Add(faction);
        var leader = CharacterOps.Mint(state, pr.ActorId,
            CharacterRole.FactionLeader, id,
            state.Config.Character.RulerMintAgeFraction);
        faction.LeaderCharacterId = leader.Id;
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FactionFormed, new[] { pr.ActorId },
            state.Actors[pr.ActorId].Seat, Magnitude: 1.0, Valence: -0.2,
            EventVisibility.Regional,
            new FactionFormedPayload(id, name, (int)basis, pr.ActorId)));
        return faction;
    }

    // ---- per-epoch update ----

    private static void Update(SimState state, PolityRecord pr,
                               PolityScan scan, ref int dissolved)
    {
        var knobs = state.Config.Faction;
        var interior = pr.Interior!;
        var species = state.Skeleton.Species[pr.SpeciesId];
        var form = GovernmentForms.Get(interior.FormId);
        int years = state.Config.Sim.YearsPerEpoch;

        foreach (var faction in state.Factions)               // id order (P6)
        {
            if (!faction.Active || faction.PolityId != pr.ActorId) continue;

            // consume this epoch's appeasement up front — every exit from
            // this loop body (dissolution included) must leave them zeroed
            double paid = faction.PaidThisEpoch;
            double demand = faction.DemandThisEpoch;
            faction.PaidThisEpoch = 0;
            faction.DemandThisEpoch = 0;

            // strength: population share by basis + wealth + patron renown
            double share = faction.Basis switch
            {
                FactionBasis.Ideological => scan.DissenterShare,
                FactionBasis.Cultural => CultureShare(state, pr, faction.ContextId,
                                                      scan.PopTotal),
                FactionBasis.Regional => scan.FrontierShare,
                FactionBasis.Military => 0.0,   // patron networks, not masses
                FactionBasis.Sacral => scan.SacralShare,
                _ => 0.0,
            };
            var leader = faction.LeaderCharacterId >= 0
                ? state.Characters[faction.LeaderCharacterId] : null;
            if (leader is { Alive: false })
            {
                leader = CharacterOps.Mint(state, pr.ActorId,
                    CharacterRole.FactionLeader, faction.Id,
                    state.Config.Character.RulerMintAgeFraction);
                faction.LeaderCharacterId = leader.Id;
            }
            double patron = leader != null
                ? knobs.PatronRenownWeight * (leader.Renown + 1) : 0;
            if (faction.Basis == FactionBasis.Military)
                patron += knobs.PatronRenownWeight * scan.CommandRenown;
            faction.Strength = Clamp01(share
                + knobs.WealthStrengthWeight * faction.Wealth
                  / (Math.Max(0, pr.Credits) + faction.Wealth + 1)
                + patron);
            faction.Militancy = Clamp01(0.4 * (leader?.Boldness ?? 0.5)
                + 0.3 * species.Militancy + 0.3 * Math.Min(1.0, faction.Grievance));

            // a spent interest dissolves: wealth returns to the people (a
            // merchant faction watching a live niche is exempt — its base
            // is the opportunity, not the masses)
            if (faction.Strength < knobs.DissolveStrengthFloor
                && faction.NichePersistenceYears <= 0)
            {
                Dissolve(state, pr, faction);
                dissolved++;
                continue;
            }

            // ideological pull on the official line, tolerance-gated
            if (faction.IdeologyTarget != null)
            {
                double pull = Math.Min(knobs.MaxBudgetPressure,
                    knobs.PressureRatePerYear * years * faction.Strength
                    * form.FactionTolerance);
                for (int ax = 0; ax < 4; ax++)
                    interior.OfficialIdeology[ax] +=
                        (faction.IdeologyTarget[ax]
                         - interior.OfficialIdeology[ax]) * pull;
            }

            // grievance settles against this epoch's appeasement
            double appeased = demand <= 0 ? 1.0 : Math.Min(1.0, paid / demand);
            faction.Grievance = Math.Max(0.0, faction.Grievance
                + knobs.GrievancePerYear * years * faction.Strength
                  * (1.0 - appeased) * (1.0 - 0.5 * form.FactionTolerance)
                - knobs.GrievanceDecayPerYear * years * appeased);
        }
    }

    private static double CultureShare(SimState state, PolityRecord pr,
                                       int cultureId, double popTotal)
    {
        if (popTotal <= 0) return 0;
        double share = 0;
        foreach (var s in state.Segments)
            if (s.Size > 0 && s.CultureId == cultureId
                && state.Ports[s.PortId].OwnerActorId == pr.ActorId)
                share += s.Size;
        return share / popTotal;
    }

    /// <summary>Dissolution: the war chest returns to the polity's segments
    /// pro-rata (conserved), the leader steps down into notability. Public
    /// since slice H — mergers dissolve the parents' politics.</summary>
    public static void Dissolve(SimState state, PolityRecord pr, Faction faction)
    {
        faction.Active = false;
        double popTotal = 0;
        foreach (var s in state.Segments)
            if (s.Size > 0 && state.Ports[s.PortId].OwnerActorId == pr.ActorId)
                popTotal += s.Size;
        if (faction.Wealth > 0 && popTotal > 0)
        {
            foreach (var s in state.Segments)
                if (s.Size > 0
                    && state.Ports[s.PortId].OwnerActorId == pr.ActorId)
                    s.Wealth += faction.Wealth * s.Size / popTotal;
            faction.Wealth = 0;
        }
        if (faction.LeaderCharacterId >= 0)
        {
            var leader = state.Characters[faction.LeaderCharacterId];
            if (leader.Alive)
            {
                leader.Role = CharacterRole.Notable;
                leader.InstitutionId = -1;
            }
        }
        state.Staged.Add(new StagedEvent(ClockStratum.Generational,
            WorldEventType.FactionDissolved, new[] { pr.ActorId },
            state.Actors[pr.ActorId].Seat, Magnitude: 1.0, Valence: 0.2,
            EventVisibility.Regional,
            new FactionDissolvedPayload(faction.Id, faction.Name)));
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
