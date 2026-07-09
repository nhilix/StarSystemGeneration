using System.Collections.Generic;
using System.Text;
using StarGen.Core.Galaxy;
using static System.FormattableString;

namespace StarGen.Core.Substrate;

/// <summary>Deterministic text rendering of the substrate catalogs — the
/// REPL's `goods` / `infra` surface. Invariant-culture throughout, fixed
/// iteration order: the eyeball gate reads this against the design tables
/// (substrate/commodities.md, infrastructure.md).</summary>
public static class SubstrateView
{
    private static readonly IReadOnlyDictionary<GoodId, string> RawSource =
        new Dictionary<GoodId, string>
        {
            [GoodId.Provisions] = "biospheres (embodiment-relative)",
            [GoodId.Ore] = "mineral-rich cells, belts",
            [GoodId.Volatiles] = "gas giants, ice worlds",
            [GoodId.Organics] = "rich biospheres",
            [GoodId.Exotics] = "precursor sites, anomalies",
        };

    public static string RenderGoods()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"goods catalog — {Goods.All.Count} (substrate/commodities.md)"));
        sb.AppendLine(" id  name             tier       recipes / source terrain");
        foreach (var g in Goods.All)
        {
            string detail = g.Tier == GoodTier.Raw
                ? "extracted: " + RawSource[g.Id]
                : string.Join(" · ", RecipeLines(g));
            sb.AppendLine(Invariant($" {(int)g.Id,2}  {g.Name,-16} {TierName(g.Tier),-10} {detail}"));
        }
        sb.AppendLine();
        sb.AppendLine("grade bands: crude <0.25 · standard <0.45 · fine <0.65 · advanced <0.80"
            + Invariant($" · masterwork <{Grades.PrecursorFloor:0.00} · precursor-grade above"));
        sb.AppendLine(Invariant($"tech ceilings: t1 {Grades.TechCeiling(1):0.00} · t2 {Grades.TechCeiling(2):0.00} · t3 {Grades.TechCeiling(3):0.00}"));
        sb.AppendLine();

        sb.AppendLine("demand — population bands per embodiment");
        foreach (Embodiment e in new[] { Embodiment.TerranAnalog, Embodiment.Aquatic,
            Embodiment.Cryophilic, Embodiment.Lithic, Embodiment.Hive, Embodiment.Machine })
        {
            var bands = new List<string>();
            foreach (PopulationBand band in new[] { PopulationBand.Subsistence,
                PopulationBand.StandardOfLiving, PopulationBand.Luxury })
                bands.Add(Invariant($"{BandName(band)} ") + Weights(DemandProfiles.Population(e, band)));
            sb.AppendLine(Invariant($"  {EmbodimentName(e),-13} (eats x{DemandProfiles.SubsistenceScale(e):0.0})  ")
                + string.Join(" | ", bands));
        }
        sb.AppendLine("institutional use-cases (after population, in priority order)");
        foreach (var u in DemandProfiles.InstitutionalUseCases)
            sb.AppendLine(Invariant($"  {u,-20} ") + Weights(DemandProfiles.Institutional(u)));
        return sb.ToString();
    }

    public static string RenderInfra()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Invariant($"infrastructure catalog — {Infrastructure.All.Count} (substrate/infrastructure.md; keystone port + 14 buildable)"));
        sb.AppendLine(Invariant($" {"id",2}  {"name",-16} {"family",-11} {"produces",-27} {"build (t1)",-42} {"yrs",3}  {"upkeep/yr",-27} {"base/yr",7}  {"labor",5}"));
        foreach (var d in Infrastructure.All)
        {
            string produces = d.Produces.Count == 0
                ? (d.Family == InfraFamily.Keystone ? "(market, lanes, reach)"
                   : d.Id == InfraTypeId.Depot ? "(storage buffering)" : "(defense, interdiction)")
                : string.Join(", ", NamesOf(d.Produces));
            sb.AppendLine(Invariant($" {(int)d.Id,2}  {d.Name,-16} {FamilyName(d.Family),-11} {produces,-27} {Costs(d.BuildCost),-42} {d.ConstructionYears,3:0}  {Costs(d.UpkeepPerYear),-27} {d.BaseOutputPerYear,7:0.#}  {d.LaborRequired,5:0.0}"));
        }
        sb.AppendLine(Invariant($"tier scaling: output x{Production.TierOutputFactor(1):0.#}/x{Production.TierOutputFactor(2):0.#}/x{Production.TierOutputFactor(3):0.#} · build cost x{Production.TierCostFactor(1):0.#}/x{Production.TierCostFactor(2):0.#}/x{Production.TierCostFactor(3):0.#}"));
        sb.AppendLine();

        sb.Append(RenderSite("sample: ore belt (wilds)",
            new CellFields(0.5, StellarLean.Balanced, 0.9, true, false),
            Site(new CellFields(0.5, StellarLean.Balanced, 0.9, true, false)),
            Embodiment.TerranAnalog));
        sb.Append(RenderSite("sample: garden world (port heart, starport, dev 2)",
            new CellFields(0.8, StellarLean.Balanced, 0.2, false, false),
            new CellSite(new CellFields(0.8, StellarLean.Balanced, 0.2, false, false),
                         Connectivity: 0.8, IsPortHeart: true, PortTier: 2,
                         DevelopmentTier: 2, IsChokepoint: false),
            Embodiment.TerranAnalog));
        sb.Append(RenderSite("sample: precursor graveyard (wilds)",
            new CellFields(0.2, StellarLean.RemnantGraveyard, 0.6, false, true),
            Site(new CellFields(0.2, StellarLean.RemnantGraveyard, 0.6, false, true)),
            Embodiment.TerranAnalog));
        return sb.ToString();
    }

    private static CellSite Site(CellFields f) =>
        new(f, Connectivity: 0.3, IsPortHeart: false, PortTier: 0,
            DevelopmentTier: 0, IsChokepoint: false);

    /// <summary>Potentials, raw grades, and the top siting scores for one
    /// cell — the REPL's per-cell evaluation (real or fixture).</summary>
    public static string RenderSite(string header, CellFields f, CellSite site,
                                    Embodiment workforce)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine(Invariant($"  fields: density {f.MeanDensity:0.00} · lean {LeanName(f.Lean)} · metallicity {f.Metallicity:0.00}")
            + (f.HasMineralAnchor ? " · mineral anchor" : "")
            + (f.HasPrecursorAnchor ? " · precursor anchor" : ""));
        double ore = Potentials.Ore(f), vol = Potentials.Volatiles(f);
        double bio = Potentials.Biosphere(f), exo = Potentials.Exotics(f);
        sb.AppendLine("  potentials:"
            + Invariant($" ore {ore:0.00} (grade {Potentials.RawGrade(ore):0.00} {BandName(Grades.BandOf(Potentials.RawGrade(ore)))})")
            + Invariant($" · volatiles {vol:0.00}")
            + Invariant($" · biosphere {bio:0.00} (x{Potentials.EmbodimentAffinity(workforce, f):0.00} {EmbodimentName(workforce)})")
            + Invariant($" · exotics {exo:0.00}"));

        var scored = new List<(double Score, InfraDef Def)>();
        foreach (var d in Infrastructure.All)
            scored.Add((Siting.Score(d.Id, site, workforce), d));
        scored.Sort((a, b) => b.Score != a.Score
            ? b.Score.CompareTo(a.Score)
            : a.Def.Id.CompareTo(b.Def.Id));   // deterministic tie-break
        sb.Append("  siting:");
        for (int i = 0; i < 5; i++)
            sb.Append(Invariant($" {scored[i].Def.Name} {scored[i].Score:0.00}") + (i < 4 ? " ·" : ""));
        sb.AppendLine();
        return sb.ToString();
    }

    private static IEnumerable<string> RecipeLines(GoodDef g)
    {
        foreach (var r in g.Recipes)
        {
            var inputs = new List<string>();
            foreach (var i in r.Inputs)
                inputs.Add(Invariant($"{Goods.Get(i.Good).Name} x{i.Quantity:0.##}"));
            yield return (r.Kind == RecipeKind.Advanced ? "adv: " : "std: ")
                + string.Join(" + ", inputs)
                + Invariant($" (base {r.GradeBase:0.00}, tech {r.MinTechTier})");
        }
    }

    private static IEnumerable<string> NamesOf(IReadOnlyList<GoodId> ids)
    {
        foreach (var id in ids) yield return Goods.Get(id).Name;
    }

    private static string Costs(IReadOnlyList<GoodQuantity> costs)
    {
        var parts = new List<string>();
        foreach (var c in costs)
            parts.Add(Invariant($"{Goods.Get(c.Good).Name} {c.Quantity:0.##}"));
        return string.Join(" + ", parts);
    }

    private static string Weights(IReadOnlyList<(GoodId Good, double Weight)> profile)
    {
        var parts = new List<string>();
        foreach (var (good, weight) in profile)
            parts.Add(Invariant($"{Goods.Get(good).Name} {weight:0.00}"));
        return string.Join(", ", parts);
    }

    private static string TierName(GoodTier t) => t switch
    {
        GoodTier.Raw => "raw",
        GoodTier.Processed => "processed",
        _ => "capital",
    };

    private static string FamilyName(InfraFamily f) => f switch
    {
        InfraFamily.Keystone => "keystone",
        InfraFamily.Extraction => "extraction",
        InfraFamily.Processing => "processing",
        InfraFamily.Heavy => "heavy",
        _ => "support",
    };

    private static string BandName(PopulationBand b) => b switch
    {
        PopulationBand.Subsistence => "subsistence",
        PopulationBand.StandardOfLiving => "SoL",
        _ => "luxury",
    };

    private static string BandName(GradeBand b) => b switch
    {
        GradeBand.Crude => "crude",
        GradeBand.Standard => "standard",
        GradeBand.Fine => "fine",
        GradeBand.Advanced => "advanced",
        GradeBand.Masterwork => "masterwork",
        _ => "precursor-grade",
    };

    private static string LeanName(StellarLean lean) => lean switch
    {
        StellarLean.Balanced => "balanced",
        StellarLean.YoungBright => "young-bright",
        StellarLean.OldDim => "old-dim",
        _ => "remnant-graveyard",
    };

    private static string EmbodimentName(Embodiment e) => e switch
    {
        Embodiment.TerranAnalog => "terran-analog",
        Embodiment.Aquatic => "aquatic",
        Embodiment.Cryophilic => "cryophilic",
        Embodiment.Lithic => "lithic",
        Embodiment.Hive => "hive",
        _ => "machine",
    };
}
