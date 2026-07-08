using System;
using System.Globalization;
using System.IO;
using StarGen.Core.Model;

namespace StarGen.Core.Galaxy;

/// <summary>Versioned artifact serialization (spec §3.1). Line-based, invariant culture,
/// fixed ordering so identical skeletons serialize byte-identically.</summary>
public static class SkeletonSerializer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string ToText(GalaxySkeleton s)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        Save(s, writer);
        return writer.ToString();
    }

    public static void Save(GalaxySkeleton s, TextWriter w)
    {
        w.NewLine = "\n";
        var c = s.Config;
        w.WriteLine($"STARGEN-SKELETON|{GalaxySkeleton.SchemaVersion}");
        w.WriteLine(string.Join("|", "CONFIG",
            c.MasterSeed.ToString(Inv), c.GalaxyRadiusCells.ToString(Inv),
            c.MeanDensityTarget.ToString("R", Inv), c.ArmCount.ToString(Inv),
            c.ArmTightness.ToString("R", Inv), c.ArmWidth.ToString("R", Inv),
            c.EpochCount.ToString(Inv), c.YearsPerEpoch.ToString(Inv),
            c.HomeworldRatePerCell.ToString("R", Inv),
            c.TraversabilityThreshold.ToString("R", Inv)));
        foreach (var sp in s.Species)
            w.WriteLine(string.Join("|", "SPECIES", sp.Id.ToString(Inv), sp.Name,
                ((int)sp.Embodiment).ToString(Inv),
                sp.Expansionism.ToString("R", Inv), sp.Cohesion.ToString("R", Inv),
                sp.Militancy.ToString("R", Inv), sp.Openness.ToString("R", Inv),
                sp.Industry.ToString("R", Inv), sp.Adaptability.ToString("R", Inv)));
        foreach (var p in s.Polities)
            w.WriteLine(string.Join("|", "POLITY", p.Id.ToString(Inv), p.Name,
                p.SpeciesId.ToString(Inv), p.CapitalQ.ToString(Inv),
                p.CapitalR.ToString(Inv), p.Extinct ? "1" : "0"));
        foreach (var cell in s.Cells)
        {
            w.WriteLine(string.Join("|", "CELL", cell.Q.ToString(Inv), cell.R.ToString(Inv),
                cell.MeanDensity.ToString("R", Inv), cell.IsVoid ? "1" : "0",
                cell.IsChokepoint ? "1" : "0", ((int)cell.Lean).ToString(Inv),
                cell.Metallicity.ToString("R", Inv), cell.OwnerPolityId.ToString(Inv),
                cell.DevelopmentTier.ToString(Inv), cell.Contested ? "1" : "0",
                cell.WarScarred ? "1" : "0"));
            foreach (var a in cell.Anchors)
                w.WriteLine(string.Join("|", "ANCHOR", cell.Q.ToString(Inv),
                    cell.R.ToString(Inv), ((int)a.Type).ToString(Inv),
                    a.Hex.Q.ToString(Inv), a.Hex.R.ToString(Inv), a.SpeciesId.ToString(Inv)));
        }
        foreach (var e in s.Events)
            w.WriteLine(string.Join("|", "EVENT", e.Epoch.ToString(Inv),
                ((int)e.Type).ToString(Inv), e.ActorPolityId.ToString(Inv),
                e.TargetPolityId.ToString(Inv), e.Q.ToString(Inv), e.R.ToString(Inv),
                e.Magnitude.ToString("R", Inv)));
        w.WriteLine("END");
    }

    public static GalaxySkeleton Load(TextReader reader)
    {
        string header = reader.ReadLine()
            ?? throw new InvalidDataException("empty skeleton artifact");
        var headerParts = header.Split('|');
        if (headerParts.Length != 2 || headerParts[0] != "STARGEN-SKELETON")
            throw new InvalidDataException("not a skeleton artifact");
        if (!int.TryParse(headerParts[1], NumberStyles.Integer, Inv, out var version))
            throw new InvalidDataException("not a skeleton artifact: non-numeric schema version");
        if (version != GalaxySkeleton.SchemaVersion)
            throw new InvalidDataException(
                $"schema version {headerParts[1]} != {GalaxySkeleton.SchemaVersion}; " +
                "keep the artifact with matching code or explicitly regenerate (spec §3.1)");

        GalaxySkeleton? s = null;
        string? line;
        while ((line = reader.ReadLine()) != null && line != "END")
        {
            try
            {
                var f = line.Split('|');
                if (f[0] != "CONFIG" && s == null)
                    throw new InvalidDataException("record before CONFIG");
                switch (f[0])
                {
                    case "CONFIG":
                        s = new GalaxySkeleton(new GalaxyConfig
                        {
                            MasterSeed = ulong.Parse(f[1], Inv), GalaxyRadiusCells = int.Parse(f[2], Inv),
                            MeanDensityTarget = double.Parse(f[3], Inv), ArmCount = int.Parse(f[4], Inv),
                            ArmTightness = double.Parse(f[5], Inv), ArmWidth = double.Parse(f[6], Inv),
                            EpochCount = int.Parse(f[7], Inv), YearsPerEpoch = int.Parse(f[8], Inv),
                            HomeworldRatePerCell = double.Parse(f[9], Inv),
                            TraversabilityThreshold = double.Parse(f[10], Inv),
                        });
                        break;
                    case "SPECIES":
                        s!.Species.Add(new SpeciesProfile
                        {
                            Id = int.Parse(f[1], Inv), Name = f[2],
                            Embodiment = (Embodiment)int.Parse(f[3], Inv),
                            Expansionism = double.Parse(f[4], Inv), Cohesion = double.Parse(f[5], Inv),
                            Militancy = double.Parse(f[6], Inv), Openness = double.Parse(f[7], Inv),
                            Industry = double.Parse(f[8], Inv), Adaptability = double.Parse(f[9], Inv),
                        });
                        break;
                    case "POLITY":
                        s!.Polities.Add(new Polity
                        {
                            Id = int.Parse(f[1], Inv), Name = f[2], SpeciesId = int.Parse(f[3], Inv),
                            CapitalQ = int.Parse(f[4], Inv), CapitalR = int.Parse(f[5], Inv),
                            Extinct = f[6] == "1",
                        });
                        break;
                    case "CELL":
                        var cell = s!.CellAt(new HexCoordinate(int.Parse(f[1], Inv), int.Parse(f[2], Inv)));
                        cell.MeanDensity = double.Parse(f[3], Inv);
                        cell.IsVoid = f[4] == "1";
                        cell.IsChokepoint = f[5] == "1";
                        cell.Lean = (StellarLean)int.Parse(f[6], Inv);
                        cell.Metallicity = double.Parse(f[7], Inv);
                        cell.OwnerPolityId = int.Parse(f[8], Inv);
                        cell.DevelopmentTier = int.Parse(f[9], Inv);
                        cell.Contested = f[10] == "1";
                        cell.WarScarred = f[11] == "1";
                        break;
                    case "ANCHOR":
                        s!.CellAt(new HexCoordinate(int.Parse(f[1], Inv), int.Parse(f[2], Inv))).Anchors.Add(new Anchor
                        {
                            Type = (AnchorType)int.Parse(f[3], Inv),
                            Hex = new HexCoordinate(int.Parse(f[4], Inv), int.Parse(f[5], Inv)),
                            SpeciesId = int.Parse(f[6], Inv),
                        });
                        break;
                    case "EVENT":
                        s!.Events.Add(new GalaxyEvent
                        {
                            Epoch = int.Parse(f[1], Inv), Type = (GalaxyEventType)int.Parse(f[2], Inv),
                            ActorPolityId = int.Parse(f[3], Inv), TargetPolityId = int.Parse(f[4], Inv),
                            Q = int.Parse(f[5], Inv), R = int.Parse(f[6], Inv),
                            Magnitude = double.Parse(f[7], Inv),
                        });
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException
                or NullReferenceException or OverflowException)
            {
                throw new InvalidDataException($"malformed skeleton artifact at line: {line}", ex);
            }
        }
        return s ?? throw new InvalidDataException("artifact missing CONFIG line");
    }
}
