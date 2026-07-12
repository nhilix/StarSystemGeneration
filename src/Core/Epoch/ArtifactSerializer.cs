using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>The layer-sectioned world-state artifact (frame/system-map.md
/// §Artifact discipline, narrative/handoff.md): each level's state is a
/// section with its own schema version; both configs stamped. Line-based,
/// invariant culture, "\n" newlines, fixed ordering — identical state
/// serializes byte-identically. The hex tier is never persisted; transients
/// (perception views, staged events, decisions, the phase trace) are not
/// state. Controllers reattach on load. Slice D: config/actors/segments at
/// v2 (knob families, standing policies + credits, identity layers) and the
/// appended markets layer (markets, cultures, located stockpiles, loans).</summary>
public static class ArtifactSerializer
{
    private const string Header = "STARGEN-EPOCH|1";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Layer names and schema versions, in artifact order — new
    /// layers append, never reorder.</summary>
    private static readonly (string Name, int Version)[] Layers =
    {
        ("config", 6), ("clock", 1), ("raster", 2), ("species", 1),
        ("actors", 6), ("ports", 2), ("lanes", 3), ("facilities", 2),
        ("fleets", 2), ("segments", 2), ("events", 1), ("markets", 2),
        ("features", 1), ("origins", 2), ("precursors", 1), ("interior", 6),
        ("corporations", 3), ("relations", 5), ("wars", 2), ("belief", 1),
        ("pulses", 1), ("pois", 1), ("plagues", 1), ("projects", 1),
    };

    public static string ToText(SimState state)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        Save(state, writer);
        return writer.ToString();
    }

    public static void Save(SimState state, TextWriter w)
    {
        w.NewLine = "\n";
        var gc = state.Skeleton.Config;
        var ec = state.Config;
        w.WriteLine(Header);

        Layer(w, "config");
        // config v5: HomeworldRatePerCell retired (polity count is causal —
        // the emergence schedule decides)
        w.WriteLine(Join("GCONFIG", gc.MasterSeed.ToString(Inv),
            gc.GalaxyRadiusCells.ToString(Inv), R(gc.MeanDensityTarget),
            gc.ArmCount.ToString(Inv), R(gc.ArmTightness), R(gc.ArmWidth),
            R(gc.ArmStrength), R(gc.CoreRadius), R(gc.DiscFalloff),
            R(gc.MineralAnchorMultiplier), R(gc.PrecursorAnchorMultiplier),
            R(gc.TraversabilityThreshold)));
        // galaxy-side genesis dials, name-sorted (config layer v4)
        foreach (var gknob in GalaxyKnobRegistry.All)
            w.WriteLine(Join("GKNOB", gknob.Name, R(gknob.Get(gc))));
        w.WriteLine(Join("ESIM", ec.MasterSeed.ToString(Inv),
            ec.Sim.YearsPerEpoch.ToString(Inv), ec.Sim.EpochCount.ToString(Inv),
            ec.Sim.GenerationYears.ToString(Inv)));
        // every calibration dial, name-sorted (the knob registry is the
        // single index — docs/TUNING.md carries the consequences)
        foreach (var knob in KnobRegistry.All)
            w.WriteLine(Join("KNOB", knob.Name, R(knob.Get(ec))));

        Layer(w, "clock");
        w.WriteLine(Join("CLOCK", state.EpochIndex.ToString(Inv),
            state.WorldYear.ToString(Inv)));

        Layer(w, "raster");
        foreach (var cell in state.Skeleton.Cells)
        {
            // raster v2 (slice F): the simulated present-day residue rides
            // the CELL line — genesis outputs are persisted, never re-run
            w.WriteLine(Join("CELL", cell.Q.ToString(Inv), cell.R.ToString(Inv),
                R(cell.MeanDensity), B(cell.IsVoid), B(cell.IsChokepoint),
                ((int)cell.Lean).ToString(Inv), R(cell.Metallicity),
                R(cell.GasFraction), R(cell.CohortYoung), R(cell.CohortMid),
                R(cell.CohortOld), R(cell.CohortRemnant), R(cell.MineralRichness),
                R(cell.SfActivity), cell.LifeViableStep.ToString(Inv),
                cell.LastSterilizedStep.ToString(Inv), R(cell.BiosphereRichness),
                R(cell.BiosphereAgeGyr)));
            foreach (var a in cell.Anchors)
                w.WriteLine(Join("ANCHOR", cell.Q.ToString(Inv), cell.R.ToString(Inv),
                    ((int)a.Type).ToString(Inv), a.Hex.Q.ToString(Inv),
                    a.Hex.R.ToString(Inv), a.SpeciesId.ToString(Inv)));
        }

        Layer(w, "species");
        foreach (var sp in state.Skeleton.Species)
            w.WriteLine(Join("SPECIES", sp.Id.ToString(Inv), Name(sp.Name),
                ((int)sp.Embodiment).ToString(Inv), R(sp.Expansionism), R(sp.Cohesion),
                R(sp.Militancy), R(sp.Openness), R(sp.Industry), R(sp.Adaptability)));

        Layer(w, "actors");
        foreach (var a in state.Actors)
        {
            // actors v5 (slice H): the retired flag rides along
            w.WriteLine(Join("ACTOR", a.Id.ToString(Inv), ((int)a.Kind).ToString(Inv),
                Name(a.Name), a.Seat.Q.ToString(Inv), a.Seat.R.ToString(Inv),
                a.EntryEpoch.ToString(Inv), B(a.Entered), B(a.Retired)));
            if (a.Policies is PolityPolicies pp)
                w.WriteLine(Join("POLICY", a.Id.ToString(Inv),
                    R(pp.Budget.Development), R(pp.Budget.Military),
                    R(pp.Budget.Research), R(pp.Budget.Expansion),
                    R(pp.Budget.Appeasement), R(pp.Budget.Reserves),
                    R(pp.TaxRate), R(pp.CharterOpenness),
                    ((int)pp.Doctrine.Posture).ToString(Inv),
                    R(pp.Doctrine.EngagementBias),
                    ((int)pp.NativePolicy).ToString(Inv),
                    DoubleMap(pp.TariffSchedule), IntMap(pp.LawCode),
                    DoubleMap(pp.ShipbuildingPriorities),
                    DoubleMap(pp.StockpileTargets), IntMap(pp.DiplomaticPostures),
                    // actors v4 (slice G): the research split rides along
                    R(pp.Research.Industrial), R(pp.Research.Military),
                    R(pp.Research.Astrogation), R(pp.Research.Life)));
            // actors v6 (slice t1): the standing plan's entries follow the
            // actor's POLICY line, in plan order (Load rebuilds them)
            if (a.Policies is PolityPolicies withPlan)
                for (int ix = 0; ix < withPlan.Plan.Entries.Count; ix++)
                {
                    var e = withPlan.Plan.Entries[ix];
                    w.WriteLine(Join("PLANE", a.Id.ToString(Inv),
                        ix.ToString(Inv), ((int)e.Kind).ToString(Inv),
                        ((int)e.Priority).ToString(Inv),
                        e.StartYear.ToString(Inv), e.TypeId.ToString(Inv),
                        e.PortId.ToString(Inv), e.Hex.Q.ToString(Inv),
                        e.Hex.R.ToString(Inv), e.Count.ToString(Inv)));
                }
        }
        foreach (var p in state.Polities)
            w.WriteLine(Join("POLITY", p.ActorId.ToString(Inv),
                p.SpeciesId.ToString(Inv), R(p.Credits),
                R(p.ExpansionPoints), R(p.DevelopmentPoints),
                R(p.EntryGradeBonus),
                // actors v6 (slice t1): trailing income rate + mobilization
                R(p.LastIncomePerYear), R(p.Mobilization)));

        Layer(w, "ports");
        foreach (var p in state.Ports)
            // ports v2 (slice I): the dead-city grace clock rides along
            w.WriteLine(Join("PORT", p.Id.ToString(Inv), p.OwnerActorId.ToString(Inv),
                p.Hex.Q.ToString(Inv), p.Hex.R.ToString(Inv), p.Tier.ToString(Inv),
                p.FoundedYear.ToString(Inv), p.LastPopulatedYear.ToString(Inv)));

        Layer(w, "lanes");
        foreach (var l in state.Lanes)
            // lanes v3 (lane economics): gate pair + the express earn-in clock
            w.WriteLine(Join("LANE", l.Id.ToString(Inv), l.PortAId.ToString(Inv),
                l.PortBId.ToString(Inv), l.BuiltYear.ToString(Inv),
                l.QuarantinedUntil.ToString(Inv), l.GateAId.ToString(Inv),
                l.GateBId.ToString(Inv), l.SaturatedYears.ToString(Inv)));

        Layer(w, "facilities");
        foreach (var f in state.Facilities)
            // facilities v2 (slice t1): the commissioning clock rides along
            w.WriteLine(Join("FACILITY", f.Id.ToString(Inv), f.TypeId.ToString(Inv),
                f.Tier.ToString(Inv), f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv),
                f.OwnerActorId.ToString(Inv), R(f.Condition), f.BuiltYear.ToString(Inv),
                f.CommissionedYear.ToString(Inv)));

        Layer(w, "fleets");
        // the fleet-side polity record: military treasury + the hull
        // conservation ledger (kept here so the actors layer stays v2)
        foreach (var p in state.Polities)
            if (p.MilitaryPoints != 0 || p.HullsBuilt != 0
                || p.HullsWrecked != 0 || p.HullsScrapped != 0)
                w.WriteLine(Join("NAVY", p.ActorId.ToString(Inv),
                    R(p.MilitaryPoints), p.HullsBuilt.ToString(Inv),
                    p.HullsWrecked.ToString(Inv), p.HullsScrapped.ToString(Inv)));
        foreach (var d in state.Designs)
            w.WriteLine(Join("DESIGN", d.Id.ToString(Inv),
                d.OwnerActorId.ToString(Inv), ((int)d.Role).ToString(Inv),
                ((int)d.Size).ToString(Inv), d.Mark.ToString(Inv), Name(d.Name),
                R(d.ComponentGrade), d.TechTier.ToString(Inv),
                d.DesignedYear.ToString(Inv)));
        foreach (var f in state.Fleets)
            w.WriteLine(Join("FLEET", f.Id.ToString(Inv), f.OwnerActorId.ToString(Inv),
                f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv),
                ((int)f.Posture).ToString(Inv), f.TargetId.ToString(Inv),
                f.HomePortId.ToString(Inv), R(f.Readiness),
                f.CommanderId.ToString(Inv), HullMap(f)));
        foreach (var wr in state.Wreckage)
            w.WriteLine(Join("WRECK", wr.Id.ToString(Inv), wr.Hex.Q.ToString(Inv),
                wr.Hex.R.ToString(Inv), wr.DesignId.ToString(Inv),
                wr.Hulls.ToString(Inv), wr.Year.ToString(Inv)));

        Layer(w, "segments");
        foreach (var s in state.Segments)
            w.WriteLine(Join("SEGMENT", s.Id.ToString(Inv), s.PortId.ToString(Inv),
                s.SpeciesId.ToString(Inv), s.CultureId.ToString(Inv), R(s.Size),
                R(s.SoL), R(s.Wealth), R(s.LastSubsistence),
                R(s.Ideology[0]), R(s.Ideology[1]), R(s.Ideology[2]),
                R(s.Ideology[3])));

        Layer(w, "events");
        foreach (var e in state.Log.Events)
        {
            var actors = new string[e.Actors.Count];
            for (int i = 0; i < e.Actors.Count; i++) actors[i] = e.Actors[i].ToString(Inv);
            w.WriteLine(Join("EVENT", e.Id.ToString(Inv), e.WorldYear.ToString(Inv),
                ((int)e.Stratum).ToString(Inv), ((int)e.Type).ToString(Inv),
                string.Join(";", actors), e.Location.Q.ToString(Inv),
                e.Location.R.ToString(Inv), R(e.Magnitude), R(e.Valence),
                ((int)e.Visibility).ToString(Inv), Payload(e.Payload)));
        }

        Layer(w, "markets");
        foreach (var c in state.Cultures)
            w.WriteLine(Join("CULTURE", c.Id.ToString(Inv), Name(c.Name),
                c.SpeciesId.ToString(Inv)));
        foreach (var m in state.Markets)
            for (int g = 0; g < m.Price.Length; g++)
                w.WriteLine(Join("MARKET", m.PortId.ToString(Inv), g.ToString(Inv),
                    R(m.Price[g]), R(m.Inventory[g]), R(m.InventoryGrade[g]),
                    R(m.LastCleared[g]), R(m.BlackBookDemand[g]),
                    R(m.BlackBookPrice[g])));
        // markets v2 (stage 2, spec §4b): located stockpiles replace the
        // RESERVE pool — per port, per good, banked where they physically sit
        foreach (var p in state.Ports)
            for (int g = 0; g < p.StockQty.Length; g++)
                if (p.StockQty[g] != 0)
                    w.WriteLine(Join("STOCK", p.Id.ToString(Inv),
                        g.ToString(Inv), R(p.StockQty[g]), R(p.StockGrade[g])));
        foreach (var l in state.Loans)
            w.WriteLine(Join("LOAN", l.Id.ToString(Inv),
                l.LenderActorId.ToString(Inv), l.BorrowerActorId.ToString(Inv),
                R(l.Principal), R(l.RatePerYear), l.TermYears.ToString(Inv),
                l.IssuedYear.ToString(Inv), B(l.Closed)));

        Layer(w, "features");
        foreach (var feat in state.Skeleton.Features)
            w.WriteLine(Join("FEATURE", feat.Id.ToString(Inv),
                ((int)feat.Type).ToString(Inv), Name(feat.Name), R(feat.DateGyr),
                CoordList(feat.Cells)));

        Layer(w, "origins");
        foreach (var o in state.Skeleton.Origins)
            w.WriteLine(Join("ORIGIN", o.Id.ToString(Inv),
                o.CellCoord.Q.ToString(Inv), o.CellCoord.R.ToString(Inv),
                o.Hex.Q.ToString(Inv), o.Hex.R.ToString(Inv),
                o.AbiogenesisYear.ToString(Inv), o.SapienceYear.ToString(Inv),
                o.SpaceflightYear.ToString(Inv), R(o.Richness),
                o.Setbacks.ToString(Inv), ((int)o.Era).ToString(Inv),
                o.DescendantOfWaveId.ToString(Inv),
                // origins v2 (slice H): native resolution rides along
                o.ResolvedEpoch.ToString(Inv)));

        Layer(w, "precursors");
        foreach (var wave in state.Skeleton.PrecursorWaves)
        {
            var lanes = new string[wave.Lanes.Count];
            for (int i = 0; i < wave.Lanes.Count; i++)
                lanes[i] = wave.Lanes[i].A.ToString(Inv) + ":"
                           + wave.Lanes[i].B.ToString(Inv);
            w.WriteLine(Join("WAVE", wave.Id.ToString(Inv),
                wave.OriginId.ToString(Inv), Name(wave.Name),
                ((int)wave.Class).ToString(Inv), R(wave.Vigor),
                wave.CapitalHex.Q.ToString(Inv), wave.CapitalHex.R.ToString(Inv),
                wave.RoseYear.ToString(Inv), wave.FellYear.ToString(Inv),
                ((int)wave.EndCause).ToString(Inv),
                wave.DescendantOriginId.ToString(Inv),
                CoordList(wave.Cells), CoordList(wave.PortHexes),
                lanes.Length == 0 ? "-" : string.Join(";", lanes)));
            foreach (var site in wave.Sites)
                w.WriteLine(Join("SITE", wave.Id.ToString(Inv),
                    site.Id.ToString(Inv), ((int)site.Type).ToString(Inv),
                    site.Hex.Q.ToString(Inv), site.Hex.R.ToString(Inv),
                    B(site.Dormant), site.OtherWaveId.ToString(Inv)));
        }

        Layer(w, "interior");
        foreach (var p in state.Polities)
            if (p.Interior is { } interior)
                w.WriteLine(Join("INTR", p.ActorId.ToString(Inv),
                    ((int)interior.FormId).ToString(Inv),
                    R(interior.OfficialIdeology[0]), R(interior.OfficialIdeology[1]),
                    R(interior.OfficialIdeology[2]), R(interior.OfficialIdeology[3]),
                    R(interior.Legitimacy), R(interior.Cohesion),
                    R(interior.Enforcement), R(interior.LastMeanSoL),
                    interior.RulerCharacterId.ToString(Inv),
                    interior.FoundingCultureId.ToString(Inv)));
        foreach (var c in state.Characters)
            w.WriteLine(Join("CHAR", c.Id.ToString(Inv), Name(c.Name),
                c.SpeciesId.ToString(Inv), c.CultureId.ToString(Inv),
                c.PolityId.ToString(Inv), ((int)c.Role).ToString(Inv),
                c.InstitutionId.ToString(Inv), ((int)c.Notable).ToString(Inv),
                c.BirthYear.ToString(Inv), B(c.Alive), c.DeathYear.ToString(Inv),
                R(c.IdeologyPosition[0]), R(c.IdeologyPosition[1]),
                R(c.IdeologyPosition[2]), R(c.IdeologyPosition[3]),
                R(c.Boldness), R(c.Zeal), R(c.Competence), R(c.Ambition),
                R(c.Renown), c.DynastyId.ToString(Inv)));
        foreach (var d in state.Dynasties)
            w.WriteLine(Join("DYNA", d.Id.ToString(Inv), Name(d.Name),
                d.FounderCharacterId.ToString(Inv), d.PolityId.ToString(Inv),
                R(d.Prestige)));
        foreach (var p in state.Polities)
            w.WriteLine(Join("TECH", p.ActorId.ToString(Inv),
                p.TechTier[0].ToString(Inv), p.TechTier[1].ToString(Inv),
                p.TechTier[2].ToString(Inv), p.TechTier[3].ToString(Inv),
                R(p.TechProgress[0]), R(p.TechProgress[1]),
                R(p.TechProgress[2]), R(p.TechProgress[3])));
        foreach (var fa in state.Factions)
            w.WriteLine(Join("FACT", fa.Id.ToString(Inv), Name(fa.Name),
                fa.PolityId.ToString(Inv), ((int)fa.Basis).ToString(Inv),
                fa.FormedYear.ToString(Inv), fa.ContextId.ToString(Inv),
                fa.LeaderCharacterId.ToString(Inv), B(fa.Active),
                R(fa.Strength), R(fa.Militancy), R(fa.Grievance), R(fa.Wealth),
                Vector(fa.BudgetTarget), Vector(fa.IdeologyTarget),
                fa.NicheType.ToString(Inv),
                fa.NichePersistenceYears.ToString(Inv)));

        Layer(w, "corporations");
        foreach (var c in state.Corporations)
            // corporations v3 (slice t1): trailing income rate rides along
            w.WriteLine(Join("CORP", c.Id.ToString(Inv),
                c.ActorId.ToString(Inv), Name(c.Name),
                c.HostPolityId.ToString(Inv), ((int)c.Niche).ToString(Inv),
                c.HomePortId.ToString(Inv), c.FoundedYear.ToString(Inv),
                B(c.Active), R(c.Credits),
                c.ExecutiveCharacterId.ToString(Inv),
                c.HullsBuilt.ToString(Inv), c.HullsWrecked.ToString(Inv),
                c.HullsScrapped.ToString(Inv), c.LeanYears.ToString(Inv),
                c.TargetId.ToString(Inv), R(c.LastIncomePerYear)));

        Layer(w, "relations");
        foreach (var r in state.Relations)
        {
            // relations v5 (slice J): every clock is a world-year (P7)
            w.WriteLine(Join("REL", r.PolityAId.ToString(Inv),
                r.PolityBId.ToString(Inv), r.MetYear.ToString(Inv),
                R(r.Warmth), R(r.Tension), ((int)r.Rung).ToString(Inv),
                ((int)r.OfferedRung).ToString(Inv), r.OfferedById.ToString(Inv),
                r.OfferYear.ToString(Inv), r.DynasticTies.ToString(Inv),
                r.VassalPolityId.ToString(Inv), r.RungYear.ToString(Inv),
                r.VassalSinceYear.ToString(Inv), r.LastTieYear.ToString(Inv),
                r.LastIncidentYear.ToString(Inv)));
            foreach (var c in r.Claims)
                w.WriteLine(Join("CLM", r.PolityAId.ToString(Inv),
                    r.PolityBId.ToString(Inv), ((int)c.Type).ToString(Inv),
                    c.HolderPolityId.ToString(Inv), c.SubjectId.ToString(Inv),
                    c.RaisedYear.ToString(Inv), B(c.Released),
                    c.ReleasedYear.ToString(Inv)));
        }

        Layer(w, "wars");
        foreach (var war in state.Wars)
        {
            w.WriteLine(Join("WAR", war.Id.ToString(Inv), Name(war.Name),
                war.AttackerId.ToString(Inv), war.DefenderId.ToString(Inv),
                ((int)war.Cause).ToString(Inv), war.SubjectId.ToString(Inv),
                ((int)war.Demand).ToString(Inv), war.DeclaredYear.ToString(Inv),
                B(war.Active), war.EndedYear.ToString(Inv),
                R(war.AttackerExhaustion), R(war.DefenderExhaustion),
                R(war.AttackerStrengthAtStart), R(war.DefenderStrengthAtStart),
                IntList(war.AttackerAllies), IntList(war.DefenderAllies)));
            foreach (var o in war.Objectives)
                w.WriteLine(Join("OBJ", war.Id.ToString(Inv),
                    o.Id.ToString(Inv), ((int)o.Type).ToString(Inv),
                    o.TargetId.ToString(Inv), ((int)o.Status).ToString(Inv),
                    o.SiegeYears.ToString(Inv)));
        }

        Layer(w, "belief");
        // belief v1 (slice I): the compressed believed world — snapshots
        // that refresh at news speed; LoadThenContinue must not re-survey
        foreach (var a in state.Actors)
        {
            foreach (var b in a.Beliefs.Polities.Values)   // subject-id order
                w.WriteLine(Join("PBEL", a.Id.ToString(Inv),
                    b.SubjectId.ToString(Inv), b.HeardYear.ToString(Inv),
                    R(b.Strength), R(b.DefensiveStrength),
                    PairList(b.Menu), SpecList(b.ObjectiveCandidates)));
            foreach (var b in a.Beliefs.Wars.Values)       // war-id order
                w.WriteLine(Join("WBEL", a.Id.ToString(Inv),
                    b.WarId.ToString(Inv), b.HeardYear.ToString(Inv),
                    R(b.OwnSideExhaustion), R(b.OwnSideStrengthShare),
                    b.ObjectivesTaken.ToString(Inv)));
            foreach (var b in a.Beliefs.Corporations.Values) // corp-id order
                w.WriteLine(Join("CBEL", a.Id.ToString(Inv),
                    b.CorpId.ToString(Inv), b.HeardYear.ToString(Inv),
                    R(b.Credits)));
            for (int i = 0; i < a.Beliefs.Stances.Count; i++) // subject order
                w.WriteLine(Join("STANCE", a.Id.ToString(Inv),
                    a.Beliefs.Stances.Keys[i].ToString(Inv),
                    R(a.Beliefs.Stances.Values[i])));
        }

        Layer(w, "pulses");
        foreach (var p in state.Pulses)
            w.WriteLine(Join("PULSE", p.Id.ToString(Inv),
                p.EventId.ToString(Inv), p.Origin.Q.ToString(Inv),
                p.Origin.R.ToString(Inv), p.EmitYear.ToString(Inv),
                R(p.Magnitude), DeliveryList(p.Delivered)));

        Layer(w, "pois");
        foreach (var poi in state.Pois)
            w.WriteLine(Join("POI", poi.Id.ToString(Inv),
                ((int)poi.Type).ToString(Inv), poi.Hex.Q.ToString(Inv),
                poi.Hex.R.ToString(Inv), R(poi.Magnitude),
                poi.FoundedYear.ToString(Inv), poi.SubjectId.ToString(Inv),
                poi.Detail.ToString(Inv), poi.HullsSalvaged.ToString(Inv),
                B(poi.Depleted), B(poi.Dormant),
                IntList(poi.ParticipantActorIds),
                LongList(poi.SourceEventIds)));

        Layer(w, "plagues");
        foreach (var plague in state.Plagues)
            w.WriteLine(Join("PLAGUE", plague.Id.ToString(Inv),
                Name(plague.Name), plague.OriginPortId.ToString(Inv),
                plague.StartYear.ToString(Inv), B(plague.Active),
                plague.EndedYear.ToString(Inv), R(plague.TotalDeaths),
                LongMap(plague.InfectedSince), LongMap(plague.ImmuneUntil)));

        Layer(w, "projects");
        foreach (var p in state.Projects)
        {
            var basket = new List<string>();
            for (int g = 0; g < p.PerYearBasket.Length; g++)
                if (p.PerYearBasket[g] != 0)
                    basket.Add(g.ToString(Inv) + ":" + R(p.PerYearBasket[g]));
            w.WriteLine(Join("PROJECT", p.Id.ToString(Inv),
                ((int)p.Kind).ToString(Inv), p.OwnerActorId.ToString(Inv),
                p.FunderActorId.ToString(Inv), p.PortId.ToString(Inv),
                p.Hex.Q.ToString(Inv), p.Hex.R.ToString(Inv),
                ((int)p.Priority).ToString(Inv), p.PlanOrder.ToString(Inv),
                R(p.WagesPerYear), R(p.YearsRequired), R(p.YearsDelivered),
                p.StartedYear.ToString(Inv), B(p.Completed), B(p.Cancelled),
                R(p.LastFedFraction), p.TypeId.ToString(Inv),
                p.TargetId.ToString(Inv), p.Count.ToString(Inv),
                R(p.AccumGrade), R(p.AccumGradeWeight),
                string.Join(";", basket)));
        }
        w.WriteLine("END");
    }

    /// <summary>Int-keyed long map as "k:v;k:v" in key order; "-" empty.</summary>
    private static string LongMap(
        System.Collections.Generic.SortedList<int, long> map)
    {
        if (map.Count == 0) return "-";
        var parts = new string[map.Count];
        for (int i = 0; i < map.Count; i++)
            parts[i] = map.Keys[i].ToString(Inv) + ":"
                       + map.Values[i].ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseLongMap(string field,
        System.Collections.Generic.SortedList<int, long> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            into.Add(int.Parse(part.Substring(0, colon), Inv),
                     long.Parse(part.Substring(colon + 1), Inv));
        }
    }

    /// <summary>Long list as "v;v;…"; "-" when empty.</summary>
    private static string LongList(IReadOnlyList<long> values)
    {
        if (values.Count == 0) return "-";
        var parts = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            parts[i] = values[i].ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseLongList(string field, List<long> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
            into.Add(long.Parse(part, Inv));
    }

    /// <summary>Pulse deliveries as "actor:year;…"; "-" when unheard.</summary>
    private static string DeliveryList(
        IReadOnlyList<(int ActorId, long Year)> delivered)
    {
        if (delivered.Count == 0) return "-";
        var parts = new string[delivered.Count];
        for (int i = 0; i < delivered.Count; i++)
            parts[i] = delivered[i].ActorId.ToString(Inv) + ":"
                       + delivered[i].Year.ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseDeliveryList(string field,
        List<(int ActorId, long Year)> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            into.Add((int.Parse(part.Substring(0, colon), Inv),
                      long.Parse(part.Substring(colon + 1), Inv)));
        }
    }

    /// <summary>Casus-belli menu as "cause:subject;…"; "-" when empty.</summary>
    private static string PairList(IReadOnlyList<CasusBelliOption> menu)
    {
        if (menu.Count == 0) return "-";
        var parts = new string[menu.Count];
        for (int i = 0; i < menu.Count; i++)
            parts[i] = ((int)menu[i].Cause).ToString(Inv) + ":"
                       + menu[i].SubjectId.ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParsePairList(string field, List<CasusBelliOption> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            into.Add(new CasusBelliOption(
                (CasusBelli)int.Parse(part.Substring(0, colon), Inv),
                int.Parse(part.Substring(colon + 1), Inv)));
        }
    }

    /// <summary>Objective specs as "type:target;…"; "-" when empty.</summary>
    private static string SpecList(IReadOnlyList<WarObjectiveSpec> specs)
    {
        if (specs.Count == 0) return "-";
        var parts = new string[specs.Count];
        for (int i = 0; i < specs.Count; i++)
            parts[i] = ((int)specs[i].Type).ToString(Inv) + ":"
                       + specs[i].TargetId.ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseSpecList(string field, List<WarObjectiveSpec> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            into.Add(new WarObjectiveSpec(
                (WarObjectiveType)int.Parse(part.Substring(0, colon), Inv),
                int.Parse(part.Substring(colon + 1), Inv)));
        }
    }

    /// <summary>Int list as "v;v;…"; "-" when empty.</summary>
    private static string IntList(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return "-";
        var parts = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            parts[i] = values[i].ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseIntList(string field, List<int> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
            into.Add(int.Parse(part, Inv));
    }

    /// <summary>Fixed-length double vector as "v;v;…"; "-" when null.</summary>
    private static string Vector(double[]? values)
    {
        if (values == null) return "-";
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++) parts[i] = R(values[i]);
        return string.Join(";", parts);
    }

    private static double[]? ParseVector(string field)
    {
        if (field == "-" || field.Length == 0) return null;
        var parts = field.Split(';');
        var values = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            values[i] = double.Parse(parts[i], Inv);
        return values;
    }

    /// <summary>Hex coordinate list as "q:r;q:r"; "-" when empty.</summary>
    private static string CoordList(IReadOnlyList<HexCoordinate> coords)
    {
        if (coords.Count == 0) return "-";
        var parts = new string[coords.Count];
        for (int i = 0; i < coords.Count; i++)
            parts[i] = coords[i].Q.ToString(Inv) + ":" + coords[i].R.ToString(Inv);
        return string.Join(";", parts);
    }

    private static void ParseCoordList(string field, List<HexCoordinate> into)
    {
        if (field == "-" || field.Length == 0) return;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            into.Add(new HexCoordinate(int.Parse(part.Substring(0, colon), Inv),
                int.Parse(part.Substring(colon + 1), Inv)));
        }
    }

    /// <summary>A fleet's composition as "designId:count:grade;…" in the
    /// list's design-id order; "-" when hull-less.</summary>
    private static string HullMap(FleetRecord fleet)
    {
        if (fleet.Hulls.Count == 0) return "-";
        var parts = new string[fleet.Hulls.Count];
        for (int i = 0; i < fleet.Hulls.Count; i++)
            parts[i] = fleet.Hulls[i].DesignId.ToString(Inv) + ":"
                       + fleet.Hulls[i].Count.ToString(Inv) + ":"
                       + R(fleet.Hulls[i].Grade);
        return string.Join(";", parts);
    }

    /// <summary>Int-keyed double map as "k:v;k:v" ascending; "-" when empty
    /// (fields are never blank).</summary>
    private static string DoubleMap(IReadOnlyDictionary<int, double> map)
    {
        if (map.Count == 0) return "-";
        var keys = new List<int>(map.Keys);
        keys.Sort();
        var parts = new string[keys.Count];
        for (int i = 0; i < keys.Count; i++)
            parts[i] = keys[i].ToString(Inv) + ":" + R(map[keys[i]]);
        return string.Join(";", parts);
    }

    private static string IntMap<T>(IReadOnlyDictionary<int, T> map) where T : struct
    {
        if (map.Count == 0) return "-";
        var keys = new List<int>(map.Keys);
        keys.Sort();
        var parts = new string[keys.Count];
        for (int i = 0; i < keys.Count; i++)
            parts[i] = keys[i].ToString(Inv) + ":"
                       + ((int)(object)map[keys[i]]!).ToString(Inv);
        return string.Join(";", parts);
    }

    private static Dictionary<int, double> ParseDoubleMap(string field)
    {
        var map = new Dictionary<int, double>();
        if (field == "-" || field.Length == 0) return map;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            map[int.Parse(part.Substring(0, colon), Inv)] =
                double.Parse(part.Substring(colon + 1), Inv);
        }
        return map;
    }

    private static Dictionary<int, T> ParseIntMap<T>(string field) where T : struct
    {
        var map = new Dictionary<int, T>();
        if (field == "-" || field.Length == 0) return map;
        foreach (var part in field.Split(';'))
        {
            int colon = part.IndexOf(':');
            map[int.Parse(part.Substring(0, colon), Inv)] =
                (T)(object)int.Parse(part.Substring(colon + 1), Inv);
        }
        return map;
    }

    public static SimState Load(TextReader reader)
    {
        string header = reader.ReadLine()
            ?? throw new InvalidDataException("empty epoch artifact");
        if (header != Header)
            throw new InvalidDataException("not an epoch artifact (or unknown artifact version)");

        GalaxySkeleton? skeleton = null;
        EpochSimConfig? config = null;
        SimState? state = null;
        int layerIndex = -1;
        bool ended = false;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line == "END") { ended = true; break; }
            var f = line.Split('|');
            try
            {
                if (f[0] == "LAYER")
                {
                    layerIndex++;
                    if (layerIndex >= Layers.Length)
                        throw new InvalidDataException($"unexpected layer '{f[1]}'");
                    var (name, version) = Layers[layerIndex];
                    if (f[1] != name)
                        throw new InvalidDataException(
                            $"layer '{f[1]}' where '{name}' was expected");
                    if (int.Parse(f[2], Inv) != version)
                        throw new InvalidDataException(
                            $"layer '{name}' schema version {f[2]} != {version}; keep the "
                            + "artifact with matching code or explicitly regenerate");
                    continue;
                }
                switch (f[0])
                {
                    case "GCONFIG":
                        skeleton = new GalaxySkeleton(new GalaxyConfig
                        {
                            MasterSeed = ulong.Parse(f[1], Inv),
                            GalaxyRadiusCells = int.Parse(f[2], Inv),
                            MeanDensityTarget = double.Parse(f[3], Inv),
                            ArmCount = int.Parse(f[4], Inv),
                            ArmTightness = double.Parse(f[5], Inv),
                            ArmWidth = double.Parse(f[6], Inv),
                            ArmStrength = double.Parse(f[7], Inv),
                            CoreRadius = double.Parse(f[8], Inv),
                            DiscFalloff = double.Parse(f[9], Inv),
                            MineralAnchorMultiplier = double.Parse(f[10], Inv),
                            PrecursorAnchorMultiplier = double.Parse(f[11], Inv),
                            TraversabilityThreshold = double.Parse(f[12], Inv),
                        });
                        break;
                    case "GKNOB":
                        var gknob = GalaxyKnobRegistry.Find(f[1])
                            ?? throw new InvalidDataException(
                                $"unknown galaxy knob '{f[1]}'; keep the artifact with "
                                + "matching code or explicitly regenerate");
                        gknob.Set(skeleton!.Config, double.Parse(f[2], Inv));
                        break;
                    case "ESIM":
                        config = new EpochSimConfig { MasterSeed = ulong.Parse(f[1], Inv) };
                        config.Sim.YearsPerEpoch = int.Parse(f[2], Inv);
                        config.Sim.EpochCount = int.Parse(f[3], Inv);
                        // config v6 (slice J): the generation calendar unit
                        config.Sim.GenerationYears = int.Parse(f[4], Inv);
                        break;
                    case "KNOB":
                        var knob = KnobRegistry.Find(f[1])
                            ?? throw new InvalidDataException(
                                $"unknown knob '{f[1]}'; keep the artifact with "
                                + "matching code or explicitly regenerate");
                        knob.Set(config!, double.Parse(f[2], Inv));
                        break;
                    case "CLOCK":
                        state = new SimState(config!, skeleton!)
                        {
                            EpochIndex = int.Parse(f[1], Inv),
                            WorldYear = int.Parse(f[2], Inv),
                        };
                        break;
                    case "CELL":
                        var cell = skeleton!.CellAt(new HexCoordinate(
                            int.Parse(f[1], Inv), int.Parse(f[2], Inv)));
                        cell.MeanDensity = double.Parse(f[3], Inv);
                        cell.IsVoid = f[4] == "1";
                        cell.IsChokepoint = f[5] == "1";
                        cell.Lean = (StellarLean)int.Parse(f[6], Inv);
                        cell.Metallicity = double.Parse(f[7], Inv);
                        cell.GasFraction = double.Parse(f[8], Inv);
                        cell.CohortYoung = double.Parse(f[9], Inv);
                        cell.CohortMid = double.Parse(f[10], Inv);
                        cell.CohortOld = double.Parse(f[11], Inv);
                        cell.CohortRemnant = double.Parse(f[12], Inv);
                        cell.MineralRichness = double.Parse(f[13], Inv);
                        cell.SfActivity = double.Parse(f[14], Inv);
                        cell.LifeViableStep = int.Parse(f[15], Inv);
                        cell.LastSterilizedStep = int.Parse(f[16], Inv);
                        cell.BiosphereRichness = double.Parse(f[17], Inv);
                        cell.BiosphereAgeGyr = double.Parse(f[18], Inv);
                        break;
                    case "FEATURE":
                        if (int.Parse(f[1], Inv) != skeleton!.Features.Count)
                            throw new InvalidDataException("feature ids out of order");
                        var feature = new GalacticFeature
                        {
                            Id = int.Parse(f[1], Inv),
                            Type = (GalacticFeatureType)int.Parse(f[2], Inv),
                            Name = f[3],
                            DateGyr = double.Parse(f[4], Inv),
                        };
                        ParseCoordList(f[5], feature.Cells);
                        skeleton.Features.Add(feature);
                        break;
                    case "ORIGIN":
                        if (int.Parse(f[1], Inv) != skeleton!.Origins.Count)
                            throw new InvalidDataException("origin ids out of order");
                        skeleton.Origins.Add(new SapientOrigin
                        {
                            Id = int.Parse(f[1], Inv),
                            CellCoord = new HexCoordinate(int.Parse(f[2], Inv),
                                int.Parse(f[3], Inv)),
                            Hex = new HexCoordinate(int.Parse(f[4], Inv),
                                int.Parse(f[5], Inv)),
                            AbiogenesisYear = long.Parse(f[6], Inv),
                            SapienceYear = long.Parse(f[7], Inv),
                            SpaceflightYear = long.Parse(f[8], Inv),
                            Richness = double.Parse(f[9], Inv),
                            Setbacks = int.Parse(f[10], Inv),
                            Era = (OriginEra)int.Parse(f[11], Inv),
                            DescendantOfWaveId = int.Parse(f[12], Inv),
                            ResolvedEpoch = int.Parse(f[13], Inv),
                        });
                        break;
                    case "WAVE":
                        if (int.Parse(f[1], Inv) != skeleton!.PrecursorWaves.Count)
                            throw new InvalidDataException("wave ids out of order");
                        var wave = new PrecursorWave
                        {
                            Id = int.Parse(f[1], Inv),
                            OriginId = int.Parse(f[2], Inv),
                            Name = f[3],
                            Class = (VigorClass)int.Parse(f[4], Inv),
                            Vigor = double.Parse(f[5], Inv),
                            CapitalHex = new HexCoordinate(int.Parse(f[6], Inv),
                                int.Parse(f[7], Inv)),
                            RoseYear = long.Parse(f[8], Inv),
                            FellYear = long.Parse(f[9], Inv),
                            EndCause = (WaveEndCause)int.Parse(f[10], Inv),
                            DescendantOriginId = int.Parse(f[11], Inv),
                        };
                        ParseCoordList(f[12], wave.Cells);
                        ParseCoordList(f[13], wave.PortHexes);
                        if (f[14] != "-")
                            foreach (var part in f[14].Split(';'))
                            {
                                int colon = part.IndexOf(':');
                                wave.Lanes.Add((
                                    int.Parse(part.Substring(0, colon), Inv),
                                    int.Parse(part.Substring(colon + 1), Inv)));
                            }
                        skeleton.PrecursorWaves.Add(wave);
                        break;
                    case "SITE":
                        var siteWave = skeleton!.PrecursorWaves[int.Parse(f[1], Inv)];
                        if (int.Parse(f[2], Inv) != siteWave.Sites.Count)
                            throw new InvalidDataException("site ids out of order");
                        siteWave.Sites.Add(new PrecursorSite
                        {
                            WaveId = siteWave.Id,
                            Id = int.Parse(f[2], Inv),
                            Type = (PrecursorSiteType)int.Parse(f[3], Inv),
                            Hex = new HexCoordinate(int.Parse(f[4], Inv),
                                int.Parse(f[5], Inv)),
                            Dormant = f[6] == "1",
                            OtherWaveId = int.Parse(f[7], Inv),
                        });
                        break;
                    case "ANCHOR":
                        skeleton!.CellAt(new HexCoordinate(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv))).Anchors.Add(new Anchor
                        {
                            Type = (AnchorType)int.Parse(f[3], Inv),
                            Hex = new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            SpeciesId = int.Parse(f[6], Inv),
                        });
                        break;
                    case "SPECIES":
                        skeleton!.Species.Add(new SpeciesProfile
                        {
                            Id = int.Parse(f[1], Inv), Name = f[2],
                            Embodiment = (Embodiment)int.Parse(f[3], Inv),
                            Expansionism = double.Parse(f[4], Inv),
                            Cohesion = double.Parse(f[5], Inv),
                            Militancy = double.Parse(f[6], Inv),
                            Openness = double.Parse(f[7], Inv),
                            Industry = double.Parse(f[8], Inv),
                            Adaptability = double.Parse(f[9], Inv),
                        });
                        break;
                    case "ACTOR":
                        var kind = (ActorKind)int.Parse(f[2], Inv);
                        IController controller = kind switch
                        {
                            ActorKind.Polity => new GenesisController(config!),
                            ActorKind.Corporation => new CorporateController(),
                            _ => new TrivialController(),
                        };
                        state!.Actors.Add(new Actor(int.Parse(f[1], Inv), kind, f[3],
                            new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            int.Parse(f[6], Inv), controller)
                        { Entered = f[7] == "1", Retired = f[8] == "1" });
                        break;
                    case "POLICY":
                        state!.Actors[int.Parse(f[1], Inv)].Policies = new PolityPolicies(
                            new BudgetWeights(double.Parse(f[2], Inv),
                                double.Parse(f[3], Inv), double.Parse(f[4], Inv),
                                double.Parse(f[5], Inv), double.Parse(f[6], Inv),
                                double.Parse(f[7], Inv)),
                            TaxRate: double.Parse(f[8], Inv),
                            TariffSchedule: ParseDoubleMap(f[13]),
                            LawCode: ParseIntMap<LegalityLevel>(f[14]),
                            CharterOpenness: double.Parse(f[9], Inv),
                            Doctrine: new MilitaryDoctrine(
                                (DoctrinePosture)int.Parse(f[10], Inv),
                                double.Parse(f[11], Inv)),
                            ShipbuildingPriorities: ParseDoubleMap(f[15]),
                            StockpileTargets: ParseDoubleMap(f[16]),
                            DiplomaticPostures: ParseIntMap<DiplomaticPosture>(f[17]),
                            NativePolicy: (NativePolicy)int.Parse(f[12], Inv),
                            Research: new ResearchSplit(double.Parse(f[18], Inv),
                                double.Parse(f[19], Inv), double.Parse(f[20], Inv),
                                double.Parse(f[21], Inv)),
                            Plan: StandingPlan.Empty);
                        break;
                    case "PLANE":
                        // actors v6 (slice t1): a plan entry following its
                        // actor's POLICY line — appended in file (plan) order
                        var planActor = state!.Actors[int.Parse(f[1], Inv)];
                        if (planActor.Policies is PolityPolicies planPolicies)
                        {
                            var entries = new List<PlanEntry>(
                                planPolicies.Plan.Entries)
                            {
                                new PlanEntry(
                                    (PlanEntryKind)int.Parse(f[3], Inv),
                                    (ProjectPriority)int.Parse(f[4], Inv),
                                    int.Parse(f[5], Inv), int.Parse(f[6], Inv),
                                    int.Parse(f[7], Inv),
                                    new HexCoordinate(int.Parse(f[8], Inv),
                                                      int.Parse(f[9], Inv)),
                                    int.Parse(f[10], Inv)),
                            };
                            planActor.Policies = planPolicies with
                            {
                                Plan = new StandingPlan(entries),
                            };
                        }
                        break;
                    case "POLITY":
                        state!.Polities.Add(new PolityRecord(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv))
                        {
                            Credits = double.Parse(f[3], Inv),
                            ExpansionPoints = double.Parse(f[4], Inv),
                            DevelopmentPoints = double.Parse(f[5], Inv),
                            EntryGradeBonus = double.Parse(f[6], Inv),
                            // actors v6 (slice t1): trailing income rate + mobilization
                            LastIncomePerYear = double.Parse(f[7], Inv),
                            Mobilization = double.Parse(f[8], Inv),
                        });
                        break;
                    case "PORT":
                        if (int.Parse(f[1], Inv) != state!.Ports.Count)
                            throw new InvalidDataException("port ids out of order");
                        state.Ports.Add(new Port(int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv), int.Parse(f[4], Inv)),
                            int.Parse(f[5], Inv), int.Parse(f[6], Inv))
                        { LastPopulatedYear = long.Parse(f[7], Inv) });
                        // markets parallel ports (market index == port id);
                        // MARKET records overwrite the founded prices below
                        state.Markets.Add(new Market(state.Ports.Count - 1,
                            state.Config.Economy));
                        break;
                    case "LANE":
                        state!.Lanes.Add(new Lane(int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv))
                        {
                            QuarantinedUntil = long.Parse(f[5], Inv),
                            GateAId = int.Parse(f[6], Inv),
                            GateBId = int.Parse(f[7], Inv),
                            SaturatedYears = int.Parse(f[8], Inv),
                        });
                        break;
                    case "FACILITY":
                        if (int.Parse(f[1], Inv) != state!.Facilities.Count)
                            throw new InvalidDataException("facility ids out of order");
                        state.Facilities.Add(new Facility(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            int.Parse(f[6], Inv), int.Parse(f[8], Inv))
                        {
                            Condition = double.Parse(f[7], Inv),
                            // facilities v2 (slice t1): the commissioning clock rides along
                            CommissionedYear = long.Parse(f[9], Inv),
                        });
                        break;
                    case "NAVY":
                    {
                        var pr = state!.PolityOf(int.Parse(f[1], Inv));
                        pr.MilitaryPoints = double.Parse(f[2], Inv);
                        pr.HullsBuilt = int.Parse(f[3], Inv);
                        pr.HullsWrecked = int.Parse(f[4], Inv);
                        pr.HullsScrapped = int.Parse(f[5], Inv);
                        break;
                    }
                    case "DESIGN":
                        if (int.Parse(f[1], Inv) != state!.Designs.Count)
                            throw new InvalidDataException("design ids out of order");
                        state.Designs.Add(new ShipDesign(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), (ShipRole)int.Parse(f[3], Inv),
                            (ShipSize)int.Parse(f[4], Inv), int.Parse(f[5], Inv),
                            f[6], double.Parse(f[7], Inv), int.Parse(f[8], Inv),
                            int.Parse(f[9], Inv)));
                        break;
                    case "FLEET":
                    {
                        if (int.Parse(f[1], Inv) != state!.Fleets.Count)
                            throw new InvalidDataException("fleet ids out of order");
                        var fleet = new FleetRecord(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv), int.Parse(f[4], Inv)))
                        {
                            Posture = (FleetPosture)int.Parse(f[5], Inv),
                            TargetId = int.Parse(f[6], Inv),
                            HomePortId = int.Parse(f[7], Inv),
                            Readiness = double.Parse(f[8], Inv),
                            CommanderId = int.Parse(f[9], Inv),
                        };
                        if (f[10] != "-")
                            foreach (var part in f[10].Split(';'))
                            {
                                var h = part.Split(':');
                                int designId = int.Parse(h[0], Inv);
                                // compositions are design-id sorted and
                                // reference registered designs — a tampered
                                // hull map must refuse here, not blow up
                                // mid-step in SheetOf
                                if (designId < 0 || designId >= state.Designs.Count
                                    || (fleet.Hulls.Count > 0
                                        && fleet.Hulls[fleet.Hulls.Count - 1]
                                               .DesignId >= designId))
                                    throw new InvalidDataException(
                                        "fleet hull map out of order or "
                                        + "referencing an unknown design");
                                fleet.Hulls.Add(new HullGroup(designId,
                                    int.Parse(h[1], Inv), double.Parse(h[2], Inv)));
                            }
                        state.Fleets.Add(fleet);
                        break;
                    }
                    case "WRECK":
                        if (int.Parse(f[1], Inv) != state!.Wreckage.Count)
                            throw new InvalidDataException("wreck ids out of order");
                        state.Wreckage.Add(new WreckageRecord(int.Parse(f[1], Inv),
                            new HexCoordinate(int.Parse(f[2], Inv), int.Parse(f[3], Inv)),
                            int.Parse(f[4], Inv), int.Parse(f[5], Inv),
                            int.Parse(f[6], Inv)));
                        break;
                    case "SEGMENT":
                        if (int.Parse(f[1], Inv) != state!.Segments.Count)
                            throw new InvalidDataException("segment ids out of order");
                        var segment = new PopulationSegment(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            int.Parse(f[4], Inv), double.Parse(f[5], Inv))
                        {
                            SoL = double.Parse(f[6], Inv),
                            Wealth = double.Parse(f[7], Inv),
                            LastSubsistence = double.Parse(f[8], Inv),
                        };
                        for (int ax = 0; ax < 4; ax++)
                            segment.Ideology[ax] = double.Parse(f[9 + ax], Inv);
                        state!.Segments.Add(segment);
                        break;
                    case "CULTURE":
                        state!.Cultures.Add(new Culture(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv)));
                        break;
                    case "MARKET":
                    {
                        var market = state!.Markets[int.Parse(f[1], Inv)];
                        int good = int.Parse(f[2], Inv);
                        market.Price[good] = double.Parse(f[3], Inv);
                        market.Inventory[good] = double.Parse(f[4], Inv);
                        market.InventoryGrade[good] = double.Parse(f[5], Inv);
                        market.LastCleared[good] = double.Parse(f[6], Inv);
                        market.BlackBookDemand[good] = double.Parse(f[7], Inv);
                        market.BlackBookPrice[good] = double.Parse(f[8], Inv);
                        break;
                    }
                    case "STOCK":
                    {
                        var port = state!.Ports[int.Parse(f[1], Inv)];
                        int good = int.Parse(f[2], Inv);
                        port.StockQty[good] = double.Parse(f[3], Inv);
                        port.StockGrade[good] = double.Parse(f[4], Inv);
                        break;
                    }
                    case "LOAN":
                        if (int.Parse(f[1], Inv) != state!.Loans.Count)
                            throw new InvalidDataException("loan ids out of order");
                        state.Loans.Add(new Loan(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            double.Parse(f[4], Inv), double.Parse(f[5], Inv),
                            int.Parse(f[6], Inv), int.Parse(f[7], Inv))
                        { Closed = f[8] == "1" });
                        break;
                    case "INTR":
                    {
                        var interior = new PolityInterior
                        {
                            FormId = (GovernmentFormId)int.Parse(f[2], Inv),
                            Legitimacy = double.Parse(f[7], Inv),
                            Cohesion = double.Parse(f[8], Inv),
                            Enforcement = double.Parse(f[9], Inv),
                            LastMeanSoL = double.Parse(f[10], Inv),
                            RulerCharacterId = int.Parse(f[11], Inv),
                            FoundingCultureId = int.Parse(f[12], Inv),
                        };
                        for (int ax = 0; ax < 4; ax++)
                            interior.OfficialIdeology[ax] = double.Parse(f[3 + ax], Inv);
                        state!.PolityOf(int.Parse(f[1], Inv)).Interior = interior;
                        break;
                    }
                    case "CHAR":
                    {
                        if (int.Parse(f[1], Inv) != state!.Characters.Count)
                            throw new InvalidDataException("character ids out of order");
                        var character = new Character(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv),
                            int.Parse(f[5], Inv), long.Parse(f[9], Inv))
                        {
                            Role = (CharacterRole)int.Parse(f[6], Inv),
                            InstitutionId = int.Parse(f[7], Inv),
                            Notable = (NotableType)int.Parse(f[8], Inv),
                            Alive = f[10] == "1",
                            DeathYear = long.Parse(f[11], Inv),
                            Boldness = double.Parse(f[16], Inv),
                            Zeal = double.Parse(f[17], Inv),
                            Competence = double.Parse(f[18], Inv),
                            Ambition = double.Parse(f[19], Inv),
                            Renown = double.Parse(f[20], Inv),
                            DynastyId = int.Parse(f[21], Inv),
                        };
                        for (int ax = 0; ax < 4; ax++)
                            character.IdeologyPosition[ax] =
                                double.Parse(f[12 + ax], Inv);
                        state.Characters.Add(character);
                        break;
                    }
                    case "DYNA":
                        if (int.Parse(f[1], Inv) != state!.Dynasties.Count)
                            throw new InvalidDataException("dynasty ids out of order");
                        state.Dynasties.Add(new Dynasty(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv))
                        { Prestige = double.Parse(f[5], Inv) });
                        break;
                    case "TECH":
                    {
                        var pr = state!.PolityOf(int.Parse(f[1], Inv));
                        for (int d = 0; d < 4; d++)
                        {
                            pr.TechTier[d] = int.Parse(f[2 + d], Inv);
                            pr.TechProgress[d] = double.Parse(f[6 + d], Inv);
                        }
                        break;
                    }
                    case "FACT":
                        if (int.Parse(f[1], Inv) != state!.Factions.Count)
                            throw new InvalidDataException("faction ids out of order");
                        state.Factions.Add(new Faction(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv), (FactionBasis)int.Parse(f[4], Inv),
                            long.Parse(f[5], Inv))
                        {
                            ContextId = int.Parse(f[6], Inv),
                            LeaderCharacterId = int.Parse(f[7], Inv),
                            Active = f[8] == "1",
                            Strength = double.Parse(f[9], Inv),
                            Militancy = double.Parse(f[10], Inv),
                            Grievance = double.Parse(f[11], Inv),
                            Wealth = double.Parse(f[12], Inv),
                            BudgetTarget = ParseVector(f[13]),
                            IdeologyTarget = ParseVector(f[14]),
                            NicheType = int.Parse(f[15], Inv),
                            NichePersistenceYears = int.Parse(f[16], Inv),
                        });
                        break;
                    case "CORP":
                        if (int.Parse(f[1], Inv) != state!.Corporations.Count)
                            throw new InvalidDataException("corporation ids out of order");
                        state.Corporations.Add(new Corporation(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), f[3], int.Parse(f[4], Inv),
                            (CorporateNiche)int.Parse(f[5], Inv),
                            int.Parse(f[6], Inv), long.Parse(f[7], Inv))
                        {
                            Active = f[8] == "1",
                            Credits = double.Parse(f[9], Inv),
                            ExecutiveCharacterId = int.Parse(f[10], Inv),
                            HullsBuilt = int.Parse(f[11], Inv),
                            HullsWrecked = int.Parse(f[12], Inv),
                            HullsScrapped = int.Parse(f[13], Inv),
                            LeanYears = int.Parse(f[14], Inv),
                            TargetId = int.Parse(f[15], Inv),
                            // corporations v3 (slice t1): trailing income rate rides along
                            LastIncomePerYear = double.Parse(f[16], Inv),
                        });
                        break;
                    case "REL":
                        state!.Relations.Add(new PolityRelation(
                            int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            int.Parse(f[3], Inv))
                        {
                            Warmth = double.Parse(f[4], Inv),
                            Tension = double.Parse(f[5], Inv),
                            Rung = (TreatyRung)int.Parse(f[6], Inv),
                            OfferedRung = (TreatyRung)int.Parse(f[7], Inv),
                            OfferedById = int.Parse(f[8], Inv),
                            OfferYear = int.Parse(f[9], Inv),
                            DynasticTies = int.Parse(f[10], Inv),
                            VassalPolityId = int.Parse(f[11], Inv),
                            RungYear = int.Parse(f[12], Inv),
                            VassalSinceYear = int.Parse(f[13], Inv),
                            LastTieYear = long.Parse(f[14], Inv),
                            LastIncidentYear = int.Parse(f[15], Inv),
                        });
                        break;
                    case "WAR":
                    {
                        if (int.Parse(f[1], Inv) != state!.Wars.Count)
                            throw new InvalidDataException("war ids out of order");
                        var war = new War(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv),
                            (CasusBelli)int.Parse(f[5], Inv),
                            int.Parse(f[6], Inv),
                            (WarDemand)int.Parse(f[7], Inv),
                            long.Parse(f[8], Inv))
                        {
                            Active = f[9] == "1",
                            EndedYear = long.Parse(f[10], Inv),
                            AttackerExhaustion = double.Parse(f[11], Inv),
                            DefenderExhaustion = double.Parse(f[12], Inv),
                            AttackerStrengthAtStart = double.Parse(f[13], Inv),
                            DefenderStrengthAtStart = double.Parse(f[14], Inv),
                        };
                        ParseIntList(f[15], war.AttackerAllies);
                        ParseIntList(f[16], war.DefenderAllies);
                        state.Wars.Add(war);
                        break;
                    }
                    case "OBJ":
                    {
                        var objWar = state!.Wars[int.Parse(f[1], Inv)];
                        if (int.Parse(f[2], Inv) != objWar.Objectives.Count)
                            throw new InvalidDataException(
                                "objective ids out of order");
                        objWar.Objectives.Add(new WarObjective(
                            int.Parse(f[2], Inv),
                            (WarObjectiveType)int.Parse(f[3], Inv),
                            int.Parse(f[4], Inv))
                        {
                            Status = (ObjectiveStatus)int.Parse(f[5], Inv),
                            SiegeYears = int.Parse(f[6], Inv),
                        });
                        break;
                    }
                    case "CLM":
                    {
                        var rel = state!.RelationOf(int.Parse(f[1], Inv),
                                int.Parse(f[2], Inv))
                            ?? throw new InvalidDataException(
                                "claim on an unknown relation");
                        rel.Claims.Add(new RelationClaim(
                            (ClaimType)int.Parse(f[3], Inv),
                            int.Parse(f[4], Inv), int.Parse(f[5], Inv),
                            long.Parse(f[6], Inv))
                        {
                            Released = f[7] == "1",
                            ReleasedYear = long.Parse(f[8], Inv),
                        });
                        break;
                    }
                    case "PBEL":
                    {
                        var belief = new PolityBelief(int.Parse(f[2], Inv))
                        {
                            HeardYear = long.Parse(f[3], Inv),
                            Strength = double.Parse(f[4], Inv),
                            DefensiveStrength = double.Parse(f[5], Inv),
                        };
                        ParsePairList(f[6], belief.Menu);
                        ParseSpecList(f[7], belief.ObjectiveCandidates);
                        state!.Actors[int.Parse(f[1], Inv)].Beliefs.Polities
                            .Add(belief.SubjectId, belief);
                        break;
                    }
                    case "WBEL":
                    {
                        var belief = new WarBelief(int.Parse(f[2], Inv))
                        {
                            HeardYear = long.Parse(f[3], Inv),
                            OwnSideExhaustion = double.Parse(f[4], Inv),
                            OwnSideStrengthShare = double.Parse(f[5], Inv),
                            ObjectivesTaken = int.Parse(f[6], Inv),
                        };
                        state!.Actors[int.Parse(f[1], Inv)].Beliefs.Wars
                            .Add(belief.WarId, belief);
                        break;
                    }
                    case "CBEL":
                    {
                        var belief = new CorpBelief(int.Parse(f[2], Inv))
                        {
                            HeardYear = long.Parse(f[3], Inv),
                            Credits = double.Parse(f[4], Inv),
                        };
                        state!.Actors[int.Parse(f[1], Inv)].Beliefs.Corporations
                            .Add(belief.CorpId, belief);
                        break;
                    }
                    case "POI":
                    {
                        if (int.Parse(f[1], Inv) != state!.Pois.Count)
                            throw new InvalidDataException(
                                "poi ids out of order");
                        var poi = new PoiRecord(int.Parse(f[1], Inv),
                            (PoiType)int.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv),
                                              int.Parse(f[4], Inv)),
                            double.Parse(f[5], Inv), long.Parse(f[6], Inv),
                            int.Parse(f[7], Inv), int.Parse(f[8], Inv))
                        {
                            HullsSalvaged = int.Parse(f[9], Inv),
                            Depleted = f[10] == "1",
                            Dormant = f[11] == "1",
                        };
                        ParseIntList(f[12], poi.ParticipantActorIds);
                        ParseLongList(f[13], poi.SourceEventIds);
                        state.Pois.Add(poi);
                        break;
                    }
                    case "PLAGUE":
                    {
                        if (int.Parse(f[1], Inv) != state!.Plagues.Count)
                            throw new InvalidDataException(
                                "plague ids out of order");
                        var plague = new Plague(int.Parse(f[1], Inv), f[2],
                            int.Parse(f[3], Inv), long.Parse(f[4], Inv))
                        {
                            Active = f[5] == "1",
                            EndedYear = long.Parse(f[6], Inv),
                            TotalDeaths = double.Parse(f[7], Inv),
                        };
                        ParseLongMap(f[8], plague.InfectedSince);
                        ParseLongMap(f[9], plague.ImmuneUntil);
                        state.Plagues.Add(plague);
                        break;
                    }
                    case "STANCE":
                        state!.Actors[int.Parse(f[1], Inv)].Beliefs.Stances
                            .Add(int.Parse(f[2], Inv), double.Parse(f[3], Inv));
                        break;
                    case "PROJECT":
                    {
                        if (int.Parse(f[1], Inv) != state!.Projects.Count)
                            throw new InvalidDataException(
                                "project ids out of order");
                        var project = new Project(int.Parse(f[1], Inv),
                            (ProjectKind)int.Parse(f[2], Inv),
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv),
                            int.Parse(f[5], Inv),
                            new HexCoordinate(int.Parse(f[6], Inv),
                                int.Parse(f[7], Inv)),
                            double.Parse(f[11], Inv), int.Parse(f[13], Inv))
                        {
                            Priority = (ProjectPriority)int.Parse(f[8], Inv),
                            PlanOrder = int.Parse(f[9], Inv),
                            WagesPerYear = double.Parse(f[10], Inv),
                            YearsDelivered = double.Parse(f[12], Inv),
                            Completed = f[14] == "1",
                            Cancelled = f[15] == "1",
                            LastFedFraction = double.Parse(f[16], Inv),
                            TypeId = int.Parse(f[17], Inv),
                            TargetId = int.Parse(f[18], Inv),
                            Count = int.Parse(f[19], Inv),
                            AccumGrade = double.Parse(f[20], Inv),
                            AccumGradeWeight = double.Parse(f[21], Inv),
                        };
                        if (f[22].Length > 0)
                            foreach (var part in f[22].Split(';'))
                            {
                                int colon = part.IndexOf(':');
                                project.PerYearBasket[
                                    int.Parse(part.Substring(0, colon), Inv)] =
                                    double.Parse(part.Substring(colon + 1), Inv);
                            }
                        state.Projects.Add(project);
                        break;
                    }
                    case "PULSE":
                    {
                        if (int.Parse(f[1], Inv) != state!.Pulses.Count)
                            throw new InvalidDataException(
                                "pulse ids out of order");
                        var pulse = new NewsPulse(int.Parse(f[1], Inv),
                            long.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv),
                                              int.Parse(f[4], Inv)),
                            long.Parse(f[5], Inv), double.Parse(f[6], Inv));
                        ParseDeliveryList(f[7], pulse.Delivered);
                        state.Pulses.Add(pulse);
                        break;
                    }
                    case "EVENT":
                        var actorParts = f[5].Length == 0
                            ? new string[0] : f[5].Split(';');
                        var actors = new int[actorParts.Length];
                        for (int i = 0; i < actorParts.Length; i++)
                            actors[i] = int.Parse(actorParts[i], Inv);
                        var appended = state!.Log.Append(long.Parse(f[2], Inv),
                            (ClockStratum)int.Parse(f[3], Inv),
                            (WorldEventType)int.Parse(f[4], Inv), actors,
                            new HexCoordinate(int.Parse(f[6], Inv), int.Parse(f[7], Inv)),
                            double.Parse(f[8], Inv), double.Parse(f[9], Inv),
                            (EventVisibility)int.Parse(f[10], Inv),
                            ParsePayload(f, 11));
                        if (appended.Id != long.Parse(f[1], Inv))
                            throw new InvalidDataException("event ids out of order");
                        break;
                    default:
                        throw new InvalidDataException($"unknown record '{f[0]}'");
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException
                or NullReferenceException or OverflowException or KeyNotFoundException)
            {
                throw new InvalidDataException($"malformed epoch artifact at line: {line}", ex);
            }
        }
        if (!ended || layerIndex != Layers.Length - 1)
            throw new InvalidDataException(
                "truncated epoch artifact: every layer and the END sentinel are required");
        if (state == null)
            throw new InvalidDataException("epoch artifact missing config/clock layers");
        return state;
    }

    private static void Layer(TextWriter w, string name)
    {
        foreach (var (n, v) in Layers)
            if (n == name) { w.WriteLine(Join("LAYER", n, v.ToString(Inv))); return; }
        throw new InvalidOperationException($"unregistered layer '{name}'");
    }

    private static string Payload(EventPayload? p) => p switch
    {
        null => "none",
        DwarfGalaxyMergedPayload e => Join("dwarfGalaxyMerged",
            e.FeatureId.ToString(Inv), Name(e.Name), R(e.Mass)),
        AgnIgnitedPayload e => Join("agnIgnited", e.FeatureId.ToString(Inv),
            e.WaveRadiusCells.ToString(Inv)),
        GlobularFormedPayload e => Join("globularFormed",
            e.FeatureId.ToString(Inv), Name(e.Name)),
        FirstLifePayload => "firstLife",
        PrecursorWaveRosePayload e => Join("precursorWaveRose",
            e.WaveId.ToString(Inv), Name(e.Name), e.VigorClass.ToString(Inv)),
        PrecursorWaveFellPayload e => Join("precursorWaveFell",
            e.WaveId.ToString(Inv), Name(e.Name), e.EndCause.ToString(Inv),
            e.ExtentCells.ToString(Inv)),
        PrecursorContactPayload e => Join("precursorContact",
            e.WaveAId.ToString(Inv), e.WaveBId.ToString(Inv),
            e.Resolution.ToString(Inv)),
        SapienceEmergedPayload e => Join("sapienceEmerged", e.OriginId.ToString(Inv)),
        SpaceflightReachedPayload e => Join("spaceflightReached",
            e.OriginId.ToString(Inv)),
        PolityEmergedPayload e => Join("polityEmerged", Name(e.PolityName)),
        PortEstablishedPayload e => Join("portEstablished", Name(e.PolityName),
            e.PortId.ToString(Inv)),
        LaneOpenedPayload e => Join("laneOpened", e.PortAId.ToString(Inv),
            e.PortBId.ToString(Inv)),
        PortTierRaisedPayload e => Join("portTierRaised", e.PortId.ToString(Inv),
            e.NewTier.ToString(Inv)),
        FamineStruckPayload e => Join("famineStruck", e.PortId.ToString(Inv),
            R(e.Shortfall)),
        FacilityBuiltPayload e => Join("facilityBuilt", e.FacilityId.ToString(Inv),
            e.TypeId.ToString(Inv), e.Tier.ToString(Inv)),
        LoanIssuedPayload e => Join("loanIssued", e.LoanId.ToString(Inv),
            e.LenderActorId.ToString(Inv), e.BorrowerActorId.ToString(Inv),
            R(e.Principal)),
        LoanDefaultedPayload e => Join("loanDefaulted", e.LoanId.ToString(Inv),
            e.LenderActorId.ToString(Inv), e.BorrowerActorId.ToString(Inv)),
        MigrationWavePayload e => Join("migrationWave", e.FromPortId.ToString(Inv),
            e.ToPortId.ToString(Inv), R(e.Size)),
        ShipClassLaunchedPayload e => Join("shipClassLaunched",
            e.DesignId.ToString(Inv), Name(e.Name), e.Mark.ToString(Inv)),
        FleetAttritionPayload e => Join("fleetAttrition", e.FleetId.ToString(Inv),
            e.HullsLost.ToString(Inv)),
        ConvoyDispatchedPayload e => Join("convoyDispatched", e.FleetId.ToString(Inv),
            e.FromPortId.ToString(Inv), e.TargetQ.ToString(Inv),
            e.TargetR.ToString(Inv)),
        CorporationCharteredPayload e => Join("corporationChartered",
            e.CorpId.ToString(Inv), Name(e.Name),
            e.HostPolityId.ToString(Inv), e.Niche.ToString(Inv)),
        PirateBandFormedPayload e => Join("pirateBandFormed",
            e.CorpId.ToString(Inv), Name(e.Name)),
        CorporationNationalizedPayload e => Join("corporationNationalized",
            e.CorpId.ToString(Inv), Name(e.Name), e.PolityId.ToString(Inv)),
        CorporationBankruptPayload e => Join("corporationBankrupt",
            e.CorpId.ToString(Inv), Name(e.Name)),
        NicheDiedPayload e => Join("nicheDied", e.CorpId.ToString(Inv),
            Name(e.Name), e.Niche.ToString(Inv)),
        TechAdvancedPayload e => Join("techAdvanced", e.PolityId.ToString(Inv),
            e.Domain.ToString(Inv), e.NewTier.ToString(Inv)),
        SchismDeclaredPayload e => Join("schismDeclared",
            e.FactionId.ToString(Inv), Name(e.FactionName),
            e.OldPolityId.ToString(Inv), e.NewPolityId.ToString(Inv),
            Name(e.NewPolityName), e.Ports.ToString(Inv)),
        CoupStruckPayload e => Join("coupStruck", e.CharacterId.ToString(Inv),
            Name(e.CharacterName), e.FactionId.ToString(Inv),
            Name(e.FactionName), e.PolityId.ToString(Inv), B(e.Contested)),
        RevoltCrushedPayload e => Join("revoltCrushed",
            e.CharacterId.ToString(Inv), Name(e.MartyrName),
            e.FactionId.ToString(Inv), Name(e.FactionName),
            e.PolityId.ToString(Inv)),
        GovernmentReformedPayload e => Join("governmentReformed",
            e.PolityId.ToString(Inv), e.OldForm.ToString(Inv),
            e.NewForm.ToString(Inv)),
        FactionFormedPayload e => Join("factionFormed",
            e.FactionId.ToString(Inv), Name(e.Name), e.Basis.ToString(Inv),
            e.PolityId.ToString(Inv)),
        FactionDissolvedPayload e => Join("factionDissolved",
            e.FactionId.ToString(Inv), Name(e.Name)),
        RulerAscendedPayload e => Join("rulerAscended",
            e.CharacterId.ToString(Inv), Name(e.CharacterName),
            e.PolityId.ToString(Inv), e.DynastyId.ToString(Inv)),
        CharacterDiedPayload e => Join("characterDied",
            e.CharacterId.ToString(Inv), Name(e.CharacterName),
            e.Role.ToString(Inv), e.AgeYears.ToString(Inv)),
        SuccessionCrisisPayload e => Join("successionCrisis",
            e.CharacterId.ToString(Inv), Name(e.DeadRulerName),
            e.PolityId.ToString(Inv)),
        NotableEmergedPayload e => Join("notableEmerged",
            e.CharacterId.ToString(Inv), Name(e.CharacterName),
            e.NotableType.ToString(Inv)),
        FirstContactPayload e => Join("firstContact",
            e.PolityAId.ToString(Inv), e.PolityBId.ToString(Inv),
            Name(e.PolityAName), Name(e.PolityBName)),
        ClaimRaisedPayload e => Join("claimRaised",
            e.HolderPolityId.ToString(Inv), e.AgainstPolityId.ToString(Inv),
            e.ClaimType.ToString(Inv), e.SubjectId.ToString(Inv)),
        ClaimReleasedPayload e => Join("claimReleased",
            e.HolderPolityId.ToString(Inv), e.AgainstPolityId.ToString(Inv),
            e.ClaimType.ToString(Inv), e.SubjectId.ToString(Inv)),
        TreatySignedPayload e => Join("treatySigned",
            e.PolityAId.ToString(Inv), e.PolityBId.ToString(Inv),
            Name(e.PolityAName), Name(e.PolityBName), e.Rung.ToString(Inv)),
        TreatyBrokenPayload e => Join("treatyBroken",
            e.BreakerPolityId.ToString(Inv), e.OtherPolityId.ToString(Inv),
            Name(e.BreakerName), Name(e.OtherName), e.Rung.ToString(Inv)),
        FederationFormedPayload e => Join("federationFormed",
            e.NewPolityId.ToString(Inv), Name(e.NewPolityName),
            e.ParentAId.ToString(Inv), e.ParentBId.ToString(Inv),
            Name(e.ParentAName), Name(e.ParentBName)),
        VassalageBoundPayload e => Join("vassalageBound",
            e.OverlordPolityId.ToString(Inv), e.VassalPolityId.ToString(Inv),
            Name(e.OverlordName), Name(e.VassalName)),
        VassalAbsorbedPayload e => Join("vassalAbsorbed",
            e.OverlordPolityId.ToString(Inv), e.VassalPolityId.ToString(Inv),
            Name(e.OverlordName), Name(e.VassalName)),
        VassalSecededPayload e => Join("vassalSeceded",
            e.OverlordPolityId.ToString(Inv), e.VassalPolityId.ToString(Inv),
            Name(e.OverlordName), Name(e.VassalName)),
        DynasticInstrumentPayload e => Join("dynasticInstrument",
            e.FromPolityId.ToString(Inv), e.ToPolityId.ToString(Inv),
            Name(e.FromName), Name(e.ToName), e.Instrument.ToString(Inv)),
        WarDeclaredPayload e => Join("warDeclared", e.WarId.ToString(Inv),
            Name(e.WarName), e.AttackerId.ToString(Inv),
            e.DefenderId.ToString(Inv), Name(e.AttackerName),
            Name(e.DefenderName), e.Cause.ToString(Inv),
            e.Demand.ToString(Inv)),
        BorderIncidentPayload e => Join("borderIncident",
            e.PolityAId.ToString(Inv), e.PolityBId.ToString(Inv),
            Name(e.PolityAName), Name(e.PolityBName), B(e.Loaded)),
        BattleFoughtPayload e => Join("battleFought", e.WarId.ToString(Inv),
            Name(e.WarName), e.ObjectiveType.ToString(Inv),
            e.TargetId.ToString(Inv), e.AttackerId.ToString(Inv),
            e.DefenderId.ToString(Inv), e.Outcome.ToString(Inv),
            e.AttackerLosses.ToString(Inv), e.DefenderLosses.ToString(Inv),
            e.AttackerCommanderId.ToString(Inv), Name(e.AttackerCommanderName),
            e.DefenderCommanderId.ToString(Inv), Name(e.DefenderCommanderName)),
        SiegeBegunPayload e => Join("siegeBegun", e.WarId.ToString(Inv),
            Name(e.WarName), e.PortId.ToString(Inv), Name(e.AttackerName),
            Name(e.DefenderName)),
        PortCapturedPayload e => Join("portCaptured", e.WarId.ToString(Inv),
            Name(e.WarName), e.PortId.ToString(Inv), Name(e.AttackerName),
            Name(e.DefenderName)),
        EmergenceSuppressedPayload e => Join("emergenceSuppressed",
            e.OriginId.ToString(Inv), e.HostPolityId.ToString(Inv),
            Name(e.HostName), Name(e.NativeName), e.Policy.ToString(Inv)),
        NativesIntegratedPayload e => Join("nativesIntegrated",
            e.OriginId.ToString(Inv), e.HostPolityId.ToString(Inv),
            Name(e.HostName), Name(e.NativeName)),
        PeaceSettledPayload e => Join("peaceSettled", e.WarId.ToString(Inv),
            Name(e.WarName), e.Outcome.ToString(Inv),
            e.WinnerId.ToString(Inv), Name(e.AttackerName),
            Name(e.DefenderName), e.PortsCeded.ToString(Inv),
            R(e.Reparations)),
        BattlefieldMarkedPayload e => Join("battlefieldMarked",
            e.PoiId.ToString(Inv), e.Hulls.ToString(Inv)),
        RuinsFallSilentPayload e => Join("ruinsFallSilent",
            e.PoiId.ToString(Inv), e.PortId.ToString(Inv)),
        CapitalRuinedPayload e => Join("capitalRuined", e.PoiId.ToString(Inv),
            e.PolityId.ToString(Inv), Name(e.PolityName)),
        MemorialRaisedPayload e => Join("memorialRaised",
            e.PoiId.ToString(Inv), e.Cause.ToString(Inv)),
        PrecursorSiteChartedPayload e => Join("precursorSiteCharted",
            e.PoiId.ToString(Inv), e.SiteType.ToString(Inv), B(e.Dormant),
            Name(e.WaveName)),
        PlagueOutbreakPayload e => Join("plagueOutbreak",
            e.PlagueId.ToString(Inv), Name(e.Name), e.PortId.ToString(Inv)),
        PlagueBurnedOutPayload e => Join("plagueBurnedOut",
            e.PlagueId.ToString(Inv), Name(e.Name), R(e.Deaths)),
        QuarantineImposedPayload e => Join("quarantineImposed",
            e.PolityId.ToString(Inv), e.LaneId.ToString(Inv)),
        _ => throw new InvalidOperationException(
            $"unserializable payload {p.GetType().Name} — extend the events layer"),
    };

    private static EventPayload? ParsePayload(string[] f, int at) => f[at] switch
    {
        "none" => null,
        "dwarfGalaxyMerged" => new DwarfGalaxyMergedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], double.Parse(f[at + 3], Inv)),
        "agnIgnited" => new AgnIgnitedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv)),
        "globularFormed" => new GlobularFormedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2]),
        "firstLife" => new FirstLifePayload(),
        "precursorWaveRose" => new PrecursorWaveRosePayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv)),
        "precursorWaveFell" => new PrecursorWaveFellPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv)),
        "precursorContact" => new PrecursorContactPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv)),
        "sapienceEmerged" => new SapienceEmergedPayload(int.Parse(f[at + 1], Inv)),
        "spaceflightReached" => new SpaceflightReachedPayload(int.Parse(f[at + 1], Inv)),
        "polityEmerged" => new PolityEmergedPayload(f[at + 1]),
        "portEstablished" => new PortEstablishedPayload(f[at + 1], int.Parse(f[at + 2], Inv)),
        "laneOpened" => new LaneOpenedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv)),
        "portTierRaised" => new PortTierRaisedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv)),
        "famineStruck" => new FamineStruckPayload(int.Parse(f[at + 1], Inv),
            double.Parse(f[at + 2], Inv)),
        "facilityBuilt" => new FacilityBuiltPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv)),
        "loanIssued" => new LoanIssuedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv),
            double.Parse(f[at + 4], Inv)),
        "loanDefaulted" => new LoanDefaultedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv)),
        "migrationWave" => new MigrationWavePayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), double.Parse(f[at + 3], Inv)),
        "shipClassLaunched" => new ShipClassLaunchedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv)),
        "fleetAttrition" => new FleetAttritionPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv)),
        "convoyDispatched" => new ConvoyDispatchedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv),
            int.Parse(f[at + 4], Inv)),
        "corporationChartered" => new CorporationCharteredPayload(
            int.Parse(f[at + 1], Inv), f[at + 2], int.Parse(f[at + 3], Inv),
            int.Parse(f[at + 4], Inv)),
        "pirateBandFormed" => new PirateBandFormedPayload(
            int.Parse(f[at + 1], Inv), f[at + 2]),
        "corporationNationalized" => new CorporationNationalizedPayload(
            int.Parse(f[at + 1], Inv), f[at + 2], int.Parse(f[at + 3], Inv)),
        "corporationBankrupt" => new CorporationBankruptPayload(
            int.Parse(f[at + 1], Inv), f[at + 2]),
        "nicheDied" => new NicheDiedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv)),
        "techAdvanced" => new TechAdvancedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv)),
        "schismDeclared" => new SchismDeclaredPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv),
            f[at + 5], int.Parse(f[at + 6], Inv)),
        "coupStruck" => new CoupStruckPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), f[at + 4],
            int.Parse(f[at + 5], Inv), f[at + 6] == "1"),
        "revoltCrushed" => new RevoltCrushedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), f[at + 4],
            int.Parse(f[at + 5], Inv)),
        "governmentReformed" => new GovernmentReformedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv),
            int.Parse(f[at + 3], Inv)),
        "factionFormed" => new FactionFormedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv)),
        "factionDissolved" => new FactionDissolvedPayload(
            int.Parse(f[at + 1], Inv), f[at + 2]),
        "rulerAscended" => new RulerAscendedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv)),
        "characterDied" => new CharacterDiedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), long.Parse(f[at + 4], Inv)),
        "successionCrisis" => new SuccessionCrisisPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv)),
        "notableEmerged" => new NotableEmergedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv)),
        "firstContact" => new FirstContactPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4]),
        "claimRaised" => new ClaimRaisedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv),
            int.Parse(f[at + 4], Inv)),
        "claimReleased" => new ClaimReleasedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), int.Parse(f[at + 3], Inv),
            int.Parse(f[at + 4], Inv)),
        "treatySigned" => new TreatySignedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4],
            int.Parse(f[at + 5], Inv)),
        "treatyBroken" => new TreatyBrokenPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4],
            int.Parse(f[at + 5], Inv)),
        "federationFormed" => new FederationFormedPayload(
            int.Parse(f[at + 1], Inv), f[at + 2], int.Parse(f[at + 3], Inv),
            int.Parse(f[at + 4], Inv), f[at + 5], f[at + 6]),
        "vassalageBound" => new VassalageBoundPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4]),
        "vassalAbsorbed" => new VassalAbsorbedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4]),
        "vassalSeceded" => new VassalSecededPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3], f[at + 4]),
        "dynasticInstrument" => new DynasticInstrumentPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv), f[at + 3],
            f[at + 4], int.Parse(f[at + 5], Inv)),
        "warDeclared" => new WarDeclaredPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv),
            f[at + 5], f[at + 6], int.Parse(f[at + 7], Inv),
            int.Parse(f[at + 8], Inv)),
        "borderIncident" => new BorderIncidentPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv), f[at + 3],
            f[at + 4], f[at + 5] == "1"),
        "battleFought" => new BattleFoughtPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv),
            int.Parse(f[at + 5], Inv), int.Parse(f[at + 6], Inv),
            int.Parse(f[at + 7], Inv), int.Parse(f[at + 8], Inv),
            int.Parse(f[at + 9], Inv), int.Parse(f[at + 10], Inv), f[at + 11],
            int.Parse(f[at + 12], Inv), f[at + 13]),
        "siegeBegun" => new SiegeBegunPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), f[at + 4], f[at + 5]),
        "portCaptured" => new PortCapturedPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), f[at + 4], f[at + 5]),
        "emergenceSuppressed" => new EmergenceSuppressedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv), f[at + 3],
            f[at + 4], int.Parse(f[at + 5], Inv)),
        "nativesIntegrated" => new NativesIntegratedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv), f[at + 3],
            f[at + 4]),
        "peaceSettled" => new PeaceSettledPayload(int.Parse(f[at + 1], Inv),
            f[at + 2], int.Parse(f[at + 3], Inv), int.Parse(f[at + 4], Inv),
            f[at + 5], f[at + 6], int.Parse(f[at + 7], Inv),
            double.Parse(f[at + 8], Inv)),
        "battlefieldMarked" => new BattlefieldMarkedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv)),
        "ruinsFallSilent" => new RuinsFallSilentPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv)),
        "capitalRuined" => new CapitalRuinedPayload(int.Parse(f[at + 1], Inv),
            int.Parse(f[at + 2], Inv), f[at + 3]),
        "memorialRaised" => new MemorialRaisedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv)),
        "precursorSiteCharted" => new PrecursorSiteChartedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv),
            f[at + 3] == "1", f[at + 4]),
        "plagueOutbreak" => new PlagueOutbreakPayload(
            int.Parse(f[at + 1], Inv), f[at + 2], int.Parse(f[at + 3], Inv)),
        "plagueBurnedOut" => new PlagueBurnedOutPayload(
            int.Parse(f[at + 1], Inv), f[at + 2],
            double.Parse(f[at + 3], Inv)),
        "quarantineImposed" => new QuarantineImposedPayload(
            int.Parse(f[at + 1], Inv), int.Parse(f[at + 2], Inv)),
        _ => throw new InvalidDataException($"unknown payload tag '{f[at]}'"),
    };

    private static string Join(params string[] fields) => string.Join("|", fields);

    /// <summary>Free-text fields (names) must not collide with the format's
    /// delimiters; escape before the format admits arbitrary text.</summary>
    private static string Name(string value) =>
        value.IndexOf('|') >= 0 || value.IndexOf(';') >= 0 || value.IndexOf('\n') >= 0
            ? throw new InvalidOperationException(
                $"unserializable name '{value}': may not contain | ; or newlines")
            : value;
    private static string R(double v) => v.ToString("R", Inv);
    private static string B(bool v) => v ? "1" : "0";
}
