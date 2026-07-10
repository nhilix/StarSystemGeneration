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
/// state. Controllers reattach on load. Standing policies are not yet
/// serialized — they are always PolityPolicies.Default in slice B; slice D
/// bumps the actors layer when they become real state.</summary>
public static class ArtifactSerializer
{
    private const string Header = "STARGEN-EPOCH|1";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Layer names and schema versions, in artifact order.</summary>
    private static readonly (string Name, int Version)[] Layers =
    {
        ("config", 1), ("clock", 1), ("raster", 1), ("species", 1),
        ("actors", 1), ("ports", 1), ("lanes", 1), ("facilities", 1),
        ("fleets", 1), ("segments", 1), ("events", 1),
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
        w.WriteLine(Join("GCONFIG", gc.MasterSeed.ToString(Inv),
            gc.GalaxyRadiusCells.ToString(Inv), R(gc.MeanDensityTarget),
            gc.ArmCount.ToString(Inv), R(gc.ArmTightness), R(gc.ArmWidth),
            R(gc.ArmStrength), R(gc.CoreRadius), R(gc.DiscFalloff),
            R(gc.MineralAnchorMultiplier), R(gc.PrecursorAnchorMultiplier),
            R(gc.HomeworldRatePerCell), R(gc.TraversabilityThreshold)));
        w.WriteLine(Join("ESIM", ec.MasterSeed.ToString(Inv),
            ec.Sim.YearsPerEpoch.ToString(Inv), ec.Sim.EpochCount.ToString(Inv)));
        w.WriteLine(Join("EGEN", ec.Genesis.EmergenceWindowYears.ToString(Inv)));
        w.WriteLine(Join("EECO", R(ec.Economy.WarWearinessPerYear),
            R(ec.Economy.StockpileDecayPerYear), R(ec.Economy.SubsistenceUnitsPerPopPerYear)));
        w.WriteLine(Join("EINF", ec.Infrastructure.ServiceRadiusBaseHexes.ToString(Inv),
            ec.Infrastructure.ServiceRadiusPerTierHexes.ToString(Inv),
            ec.Infrastructure.InterPortRangeBaseHexes.ToString(Inv),
            ec.Infrastructure.InterPortRangePerTierHexes.ToString(Inv),
            ec.Infrastructure.MaxPortTier.ToString(Inv),
            ec.Infrastructure.HomeworldPortTier.ToString(Inv)));
        w.WriteLine(Join("EEXP",
            R(ec.Expansion.ColonyCost), ec.Expansion.ColonizationReachHexes.ToString(Inv),
            R(ec.Expansion.PortUpgradeCostBase), R(ec.Expansion.LaneCost),
            R(ec.Expansion.HomeworldSegmentSize), R(ec.Expansion.ColonySegmentSize),
            R(ec.Expansion.SegmentGrowthPerYear), R(ec.Expansion.SegmentCapPerTier)));

        Layer(w, "clock");
        w.WriteLine(Join("CLOCK", state.EpochIndex.ToString(Inv),
            state.WorldYear.ToString(Inv)));

        Layer(w, "raster");
        foreach (var cell in state.Skeleton.Cells)
        {
            w.WriteLine(Join("CELL", cell.Q.ToString(Inv), cell.R.ToString(Inv),
                R(cell.MeanDensity), B(cell.IsVoid), B(cell.IsChokepoint),
                ((int)cell.Lean).ToString(Inv), R(cell.Metallicity)));
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
            w.WriteLine(Join("ACTOR", a.Id.ToString(Inv), ((int)a.Kind).ToString(Inv),
                Name(a.Name), a.Seat.Q.ToString(Inv), a.Seat.R.ToString(Inv),
                a.EntryEpoch.ToString(Inv), B(a.Entered)));
        foreach (var p in state.Polities)
            w.WriteLine(Join("POLITY", p.ActorId.ToString(Inv),
                p.SpeciesId.ToString(Inv), R(p.ExpansionPoints), R(p.DevelopmentPoints)));

        Layer(w, "ports");
        foreach (var p in state.Ports)
            w.WriteLine(Join("PORT", p.Id.ToString(Inv), p.OwnerActorId.ToString(Inv),
                p.Hex.Q.ToString(Inv), p.Hex.R.ToString(Inv), p.Tier.ToString(Inv),
                p.FoundedYear.ToString(Inv)));

        Layer(w, "lanes");
        foreach (var l in state.Lanes)
            w.WriteLine(Join("LANE", l.Id.ToString(Inv), l.PortAId.ToString(Inv),
                l.PortBId.ToString(Inv), l.BuiltYear.ToString(Inv)));

        Layer(w, "facilities");
        foreach (var f in state.Facilities)
            w.WriteLine(Join("FACILITY", f.Id.ToString(Inv), f.TypeId.ToString(Inv),
                f.Tier.ToString(Inv), f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv),
                f.OwnerActorId.ToString(Inv), R(f.Condition), f.BuiltYear.ToString(Inv)));

        Layer(w, "fleets");
        foreach (var f in state.Fleets)
            w.WriteLine(Join("FLEET", f.Id.ToString(Inv), f.OwnerActorId.ToString(Inv),
                f.Hex.Q.ToString(Inv), f.Hex.R.ToString(Inv)));

        Layer(w, "segments");
        foreach (var s in state.Segments)
            w.WriteLine(Join("SEGMENT", s.Id.ToString(Inv), s.PortId.ToString(Inv),
                s.SpeciesId.ToString(Inv), R(s.Size)));

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
        w.WriteLine("END");
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
                            HomeworldRatePerCell = double.Parse(f[12], Inv),
                            TraversabilityThreshold = double.Parse(f[13], Inv),
                        });
                        break;
                    case "ESIM":
                        config = new EpochSimConfig { MasterSeed = ulong.Parse(f[1], Inv) };
                        config.Sim.YearsPerEpoch = int.Parse(f[2], Inv);
                        config.Sim.EpochCount = int.Parse(f[3], Inv);
                        break;
                    case "EGEN":
                        config!.Genesis.EmergenceWindowYears = int.Parse(f[1], Inv);
                        break;
                    case "EECO":
                        config!.Economy.WarWearinessPerYear = double.Parse(f[1], Inv);
                        config.Economy.StockpileDecayPerYear = double.Parse(f[2], Inv);
                        config.Economy.SubsistenceUnitsPerPopPerYear = double.Parse(f[3], Inv);
                        break;
                    case "EINF":
                        config!.Infrastructure.ServiceRadiusBaseHexes = int.Parse(f[1], Inv);
                        config.Infrastructure.ServiceRadiusPerTierHexes = int.Parse(f[2], Inv);
                        config.Infrastructure.InterPortRangeBaseHexes = int.Parse(f[3], Inv);
                        config.Infrastructure.InterPortRangePerTierHexes = int.Parse(f[4], Inv);
                        config.Infrastructure.MaxPortTier = int.Parse(f[5], Inv);
                        config.Infrastructure.HomeworldPortTier = int.Parse(f[6], Inv);
                        break;
                    case "EEXP":
                        config!.Expansion.ColonyCost = double.Parse(f[1], Inv);
                        config.Expansion.ColonizationReachHexes = int.Parse(f[2], Inv);
                        config.Expansion.PortUpgradeCostBase = double.Parse(f[3], Inv);
                        config.Expansion.LaneCost = double.Parse(f[4], Inv);
                        config.Expansion.HomeworldSegmentSize = double.Parse(f[5], Inv);
                        config.Expansion.ColonySegmentSize = double.Parse(f[6], Inv);
                        config.Expansion.SegmentGrowthPerYear = double.Parse(f[7], Inv);
                        config.Expansion.SegmentCapPerTier = double.Parse(f[8], Inv);
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
                        // culture registry mirrors species (id == species id)
                        // until the markets layer serializes CULTURE records
                        state!.Cultures.Add(new Culture(state.Cultures.Count, f[2],
                            int.Parse(f[1], Inv)));
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
                        IController controller = kind == ActorKind.Polity
                            ? new GenesisController(config!)
                            : new TrivialController();
                        state!.Actors.Add(new Actor(int.Parse(f[1], Inv), kind, f[3],
                            new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            int.Parse(f[6], Inv), controller)
                        { Entered = f[7] == "1" });
                        break;
                    case "POLITY":
                        state!.Polities.Add(new PolityRecord(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv))
                        {
                            ExpansionPoints = double.Parse(f[3], Inv),
                            DevelopmentPoints = double.Parse(f[4], Inv),
                        });
                        break;
                    case "PORT":
                        state!.Ports.Add(new Port(int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv), int.Parse(f[4], Inv)),
                            int.Parse(f[5], Inv), int.Parse(f[6], Inv)));
                        // markets parallel ports; founded state until the
                        // markets layer round-trips prices (slice D task 7)
                        state.Markets.Add(new Market(state.Ports.Count - 1,
                            state.Config.Economy));
                        break;
                    case "LANE":
                        state!.Lanes.Add(new Lane(int.Parse(f[1], Inv), int.Parse(f[2], Inv),
                            int.Parse(f[3], Inv), int.Parse(f[4], Inv)));
                        break;
                    case "FACILITY":
                        state!.Facilities.Add(new Facility(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            int.Parse(f[6], Inv), int.Parse(f[8], Inv))
                        { Condition = double.Parse(f[7], Inv) });
                        break;
                    case "FLEET":
                        state!.Fleets.Add(new FleetRecord(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv),
                            new HexCoordinate(int.Parse(f[3], Inv), int.Parse(f[4], Inv))));
                        break;
                    case "SEGMENT":
                        // culture id == species id until segments layer v2
                        // serializes the identity layers (slice D task 7)
                        state!.Segments.Add(new PopulationSegment(int.Parse(f[1], Inv),
                            int.Parse(f[2], Inv), int.Parse(f[3], Inv),
                            int.Parse(f[3], Inv), double.Parse(f[4], Inv)));
                        break;
                    case "EVENT":
                        var actorParts = f[5].Length == 0
                            ? new string[0] : f[5].Split(';');
                        var actors = new int[actorParts.Length];
                        for (int i = 0; i < actorParts.Length; i++)
                            actors[i] = int.Parse(actorParts[i], Inv);
                        var appended = state!.Log.Append(int.Parse(f[2], Inv),
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
        _ => throw new InvalidOperationException(
            $"unserializable payload {p.GetType().Name} — extend the events layer"),
    };

    private static EventPayload? ParsePayload(string[] f, int at) => f[at] switch
    {
        "none" => null,
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
