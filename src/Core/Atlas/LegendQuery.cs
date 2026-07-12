using System.Collections.Generic;
using StarGen.Core.Substrate;

namespace StarGen.Core.Atlas;

/// <summary>How a legend entry draws its swatch: a filled area, a lane
/// stroke, an authored glyph (GlyphKey names the AtlasGlyph enum member),
/// or an additive ring.</summary>
public enum LegendSwatch { Fill, Stroke, Glyph, Ring }

/// <summary>One legend line: swatch + color + what it means. Colors come
/// from the SAME lens constants and ramps the layers draw with — the emap
/// legend-line pattern, made visual, drift-proof by construction.</summary>
public sealed record LegendEntry(LegendSwatch Swatch, Rgba Color,
                                 string Label, string? GlyphKey = null);

/// <summary>K3 (the K2 eyeball ask): every lens surfaces its vocabulary —
/// glyph shapes, color ramps, lane stroke states. Keyed by the rail's
/// lens keys; the good parameterizes the price legend's title only (bands
/// are ratio-based, good-independent).</summary>
public static class LegendQuery
{
    public static IReadOnlyList<LegendEntry> For(string lensKey, GoodId good)
    {
        switch (lensKey.ToLowerInvariant())
        {
            case "domains":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill,
                        AtlasPalette.OwnerColor(0),
                        "domain glow — one hue per polity (golden-ratio)"),
                    new LegendEntry(LegendSwatch.Fill, DomainLens.WarShade,
                        "contested overlap — at war"),
                    new LegendEntry(LegendSwatch.Fill, DomainLens.TensionShade,
                        "contested overlap — tense"),
                    new LegendEntry(LegendSwatch.Fill, DomainLens.WarmShade,
                        "contested overlap — warm"),
                    new LegendEntry(LegendSwatch.Fill, DomainLens.NeutralShade,
                        "contested overlap — neutral"),
                    new LegendEntry(LegendSwatch.Fill, AtlasPalette.Void,
                        "the wilds — dark, unclaimed"),
                };
            case "war":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill, DomainLens.WarShade,
                        "belligerent domain — accent burns"),
                    new LegendEntry(LegendSwatch.Fill, AtlasPalette.Floor,
                        "peaceful domain — fades to ash"),
                    new LegendEntry(LegendSwatch.Glyph, WarLens.StationBurn,
                        "war fleet on station (blockade/expedition)",
                        "FleetBlockade"),
                };
            case "tension":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill,
                        TensionLens.HeatColor(0.0), "cold — no live pressure"),
                    new LegendEntry(LegendSwatch.Fill,
                        TensionLens.HeatColor(0.5), "warming"),
                    new LegendEntry(LegendSwatch.Fill,
                        TensionLens.HeatColor(1.0), "ember — at the brink"),
                };
            case "lanes":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Stroke, LaneLens.OpenColor,
                        "lane open"),
                    new LegendEntry(LegendSwatch.Stroke,
                        LaneLens.QuarantinedColor, "lane quarantined"),
                    new LegendEntry(LegendSwatch.Stroke, LaneLens.SeveredColor,
                        "lane severed (blockade or dead gate)"),
                };
            case "traffic":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Stroke,
                        new Rgba(TrafficLens.LaneHue.R, TrafficLens.LaneHue.G,
                                 TrafficLens.LaneHue.B, 60),
                        "trickle — under half a trip a year"),
                    new LegendEntry(LegendSwatch.Stroke,
                        new Rgba(TrafficLens.LaneHue.R, TrafficLens.LaneHue.G,
                                 TrafficLens.LaneHue.B, 150),
                        "steady — a few trips a year"),
                    new LegendEntry(LegendSwatch.Stroke, TrafficLens.LaneHue,
                        "heavy — five or more trips a year"),
                };
            case "fleets":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "posted — freight on a lane", "FleetPosted"),
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "escort — riding a lane", "FleetEscort"),
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "patrol — sweeping a port", "FleetPatrol"),
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "blockade — the approaches cut", "FleetBlockade"),
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "expedition — under way", "FleetExpedition"),
                    new LegendEntry(LegendSwatch.Glyph, OwnerSwatch,
                        "reserve — docked at home", "FleetReserve"),
                };
            case "works":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Glyph, WorksLens.SiteAmber,
                        "construction site — cools amber→ember as it starves",
                        "WorkSite"),
                    new LegendEntry(LegendSwatch.Glyph,
                        WorksLens.FreightMoving, "freight under way",
                        "WorkFreight"),
                    new LegendEntry(LegendSwatch.Glyph,
                        WorksLens.FreightStalled, "freight STALLED — leg closed",
                        "WorkFreight"),
                    new LegendEntry(LegendSwatch.Glyph, WorksLens.ConvoyWhite,
                        "expedition convoy", "WorkConvoy"),
                };
            case "price":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(0.1),
                        "glut — under a quarter of founding"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(0.4),
                        "cheap"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(1.0),
                        "par — near founding price"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(2.0),
                        "dear"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(5.0),
                        "scarce"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(15.0),
                        "spike"),
                    new LegendEntry(LegendSwatch.Fill, PriceLens.ShadeOf(999.0),
                        "famine — 30× founding and beyond"),
                };
            case "tech":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill, TechLens.TierColor(0),
                        "bronze — astrogation t0"),
                    new LegendEntry(LegendSwatch.Fill, TechLens.TierColor(2),
                        "astrogation t2"),
                    new LegendEntry(LegendSwatch.Fill, TechLens.TierColor(4),
                        "arc-light — astrogation t4+"),
                };
            case "plague":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Glyph, PlagueLens.InfectedBurn,
                        "port infected — the strain burns", "PlagueInfected"),
                    new LegendEntry(LegendSwatch.Glyph, PlagueLens.ImmuneScar,
                        "port immune — the scar holds", "PlagueImmune"),
                    new LegendEntry(LegendSwatch.Stroke,
                        LaneLens.QuarantinedColor,
                        "quarantined approach"),
                };
            case "news":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Ring, NewsLens.Parchment,
                        "word spreading — a ring per pulse, fading with age"),
                };
            case "pois":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Glyph,
                        PoiLens.ColorOf(Epoch.PoiType.Battlefield),
                        "battlefield — salvage in the field", "PoiBattlefield"),
                    new LegendEntry(LegendSwatch.Glyph,
                        PoiLens.ColorOf(Epoch.PoiType.Ruins),
                        "ruins", "PoiRuins"),
                    new LegendEntry(LegendSwatch.Glyph,
                        PoiLens.ColorOf(Epoch.PoiType.RuinedCapital),
                        "ruined capital", "PoiRuinedCapital"),
                    new LegendEntry(LegendSwatch.Glyph,
                        PoiLens.ColorOf(Epoch.PoiType.Memorial),
                        "memorial", "PoiMemorial"),
                    new LegendEntry(LegendSwatch.Glyph,
                        PoiLens.ColorOf(Epoch.PoiType.PrecursorSite),
                        "precursor site (dim = dormant)", "PoiPrecursor"),
                };
            case "ports":
                return new[]
                {
                    new LegendEntry(LegendSwatch.Fill, OwnerSwatch,
                        "port — owner hue, brighter and larger by tier"),
                };
            default:
                if (lensKey.StartsWith("nature"))
                    return new[]
                    {
                        new LegendEntry(LegendSwatch.Fill, AtlasPalette.Floor,
                            "low — the raster's floor"),
                        new LegendEntry(LegendSwatch.Fill,
                            AtlasPalette.Ramp(new Rgba(200, 210, 230), 1.0),
                            "high — the raster's peak"),
                    };
                return System.Array.Empty<LegendEntry>();
        }
    }

    /// <summary>Stands in for "the owner's hue" on glyph rows — marks are
    /// runtime-tinted per actor (AtlasPalette.OwnerColor).</summary>
    private static readonly Rgba OwnerSwatch = new(199, 211, 234);
}
