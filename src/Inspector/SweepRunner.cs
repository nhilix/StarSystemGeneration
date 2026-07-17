using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using static System.FormattableString;

namespace StarGen.Inspector;

/// <summary>The headless knob-sweep runner (sim-health spec §4):
/// `dotnet run --project src/Inspector -- sweep experiment.json` runs a
/// baseline and named knob-variants across a seed ensemble and writes the
/// health series as CSVs under runs/sweeps/&lt;name&gt;/. Knob names
/// resolve through the registries (epoch first, then galaxy) — unknown
/// names refuse to run, the artifact-loader contract. Output is
/// deterministic: same experiment file, byte-identical CSVs.
///
/// <para>Two modes, distinguished by the experiment file:</para>
/// <list type="bullet">
/// <item><b>Depth mode</b> (<c>epochs</c>) — the conservation sweep. One
/// global epoch count, whatever clock the config says.</item>
/// <item><b>Clock mode</b> (<c>worldYears</c> + <c>clocks</c>) — the
/// clock-invariance sweep (slice MC). Runs each seed at several integration
/// steps over the SAME world-time span and reports the divergence per metric
/// per seed, nominal and real separately.</item>
/// </list>
///
/// <para>The two are mutually exclusive on purpose. A clock sweep written in
/// depth mode would apply one epoch count to every clock and silently compare
/// 40 world-years against 1000 — the exact trap that makes P7 measurements in
/// this codebase disagree with each other. In clock mode the span is the input
/// and the epoch count is derived (<see cref="ClockPlan"/>), so a mismatched
/// comparison cannot be expressed.</para></summary>
public static class SweepRunner
{
    /// <summary>One reported quantity: how to pull it out of a finished run,
    /// and which side of the nominal/real frame it sits on. That frame is the
    /// whole analytical point of the clock sweep — a purely nominal divergence
    /// and a real-economy divergence are completely different findings, and
    /// every prior investigation blurred them — so it is a column, not prose.</summary>
    private sealed record Reported(
        string Column, string Metric, string Frame, string Agg);

    private static readonly Reported[] ClockReport =
    {
        // nominal — money booked. Receipts are a per-epoch FLOW: integrate.
        new("sum_polity_receipts", "Economy.PolityReceipts", "nominal", "sum"),
        new("sum_corp_receipts", "Economy.CorpReceipts", "nominal", "sum"),
        // the gross value of actual trade — nominal like receipts and directly
        // commensurable with them, so sum_polity_receipts / sum_goods_value is
        // the churn multiple: how many times each credit of real trade gets
        // booked into a treasury. Cross-currency (see the metric's doc).
        new("sum_goods_value", "Economy.GoodsValueCleared", "nominal", "sum"),
        // already a running total on the row: read the final level, never sum
        new("fiat_issued", "Money.CumulativeFiatIssued", "nominal", "final"),
        new("steady_issued", "Money.CumulativeSteadyIssuance", "nominal", "final"),
        // real — things, not money
        // goods UNITS transacted: the real economy's THROUGHPUT, as opposed to
        // ports/population which are its EXTENT. A gross-flow metric can diverge
        // purely by counting more often; a unit count cannot, so this is the
        // column that settles whether Σ receipts measures activity or arithmetic.
        new("sum_goods_transacted", "Economy.GoodsTransacted", "real", "sum"),
        new("ports", "Settlement.Ports", "real", "final"),
        new("population", "Segment.Population", "real", "final"),
        new("mean_sol", "Segment.MeanSoL", "real", "final"),
        new("live_polities", "Polity.Live", "real", "final"),
    };

    public static int Run(string experimentPath)
    {
        if (!File.Exists(experimentPath))
        { Console.Error.WriteLine($"no experiment file at {experimentPath}"); return 1; }

        string name;
        List<ulong> seeds;
        int epochs, radius, worldYears;
        List<int> clocks;
        bool selfCheck;
        List<(string Name, Dictionary<string, double> Knobs)> variants;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(experimentPath));
            var root = doc.RootElement;
            name = root.GetProperty("name").GetString()
                ?? throw new InvalidDataException("experiment needs a name");
            seeds = new List<ulong>();
            foreach (var s in root.GetProperty("seeds").EnumerateArray())
                seeds.Add(s.GetUInt64());
            epochs = root.TryGetProperty("epochs", out var ep)
                ? ep.GetInt32() : new EpochSimConfig().Sim.EpochCount;
            radius = root.TryGetProperty("radius", out var ra)
                ? ra.GetInt32() : 21;   // the REPL's default galaxy

            // ---- clock mode ----
            worldYears = root.TryGetProperty("worldYears", out var wy)
                ? wy.GetInt32() : 0;
            clocks = new List<int>();
            if (root.TryGetProperty("clocks", out var cl))
                foreach (var c in cl.EnumerateArray()) clocks.Add(c.GetInt32());
            selfCheck = !root.TryGetProperty("selfCheck", out var sc)
                || sc.GetBoolean();

            if (worldYears > 0 && root.TryGetProperty("epochs", out _))
                throw new InvalidDataException(
                    "'epochs' and 'worldYears' are mutually exclusive — in clock "
                    + "mode the epoch count is DERIVED from the span, which is the "
                    + "only thing that keeps the clocks comparable");
            if (worldYears > 0 && clocks.Count == 0)
                throw new InvalidDataException(
                    "'worldYears' needs 'clocks' — a span with no steps to compare");
            if (clocks.Count > 0 && worldYears <= 0)
                throw new InvalidDataException(
                    "'clocks' needs 'worldYears' — without a constant span a clock "
                    + "comparison measures nothing");

            // variant map in file order, baseline always first
            variants = new List<(string, Dictionary<string, double>)>
            { ("baseline", ReadKnobs(root, "baseline")) };
            if (root.TryGetProperty("variants", out var vs))
                foreach (var v in vs.EnumerateObject())
                {
                    // a variant named baseline would silently replace the
                    // control run — refuse, like unknown knobs
                    if (v.Name == "baseline")
                    { Console.Error.WriteLine("'baseline' is reserved — the control always runs"); return 1; }
                    var merged = ReadKnobs(root, "baseline");
                    foreach (var k in v.Value.EnumerateObject())
                        merged[k.Name] = k.Value.GetDouble();
                    variants.Add((v.Name, merged));
                }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
            or InvalidOperationException or FormatException or InvalidDataException)
        {
            Console.Error.WriteLine($"bad experiment file: {ex.Message}");
            return 1;
        }
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        { Console.Error.WriteLine($"experiment name '{name}' must be a plain directory name"); return 1; }

        bool clockMode = worldYears > 0;
        if (clockMode)
        {
            // refuse a clock the span cannot integrate exactly, and refuse
            // duplicates (they would collide on output paths and quietly
            // halve the grid) — before any run starts, like unknown knobs
            var seen = new HashSet<int>();
            foreach (var c in clocks)
            {
                if (!seen.Add(c))
                { Console.Error.WriteLine($"duplicate clock {c}y"); return 1; }
                try { ClockPlan.EpochsFor(worldYears, c); }
                catch (ArgumentException ex)
                { Console.Error.WriteLine(ex.Message); return 1; }
            }
        }

        // refuse unknown names before any run starts
        foreach (var (_, knobs) in variants)
            foreach (var kn in knobs.Keys)
                if (KnobRegistry.Find(kn) == null
                    && GalaxyKnobRegistry.Find(kn) == null)
                { Console.Error.WriteLine($"unknown knob '{kn}'"); return 1; }
        foreach (var r in ClockReport)
            if (MetricRegistry.Find(r.Metric) == null)
            { Console.Error.WriteLine($"unknown metric '{r.Metric}'"); return 1; }

        string sweepDir = Path.Combine("runs", "sweeps", name);
        Directory.CreateDirectory(sweepDir);
        var manifest = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["seeds"] = seeds,
            ["radius"] = radius,
            ["variants"] = new Dictionary<string, object?>(),
        };
        if (clockMode)
        {
            manifest["worldYears"] = worldYears;
            manifest["clocks"] = clocks;
            var derived = new Dictionary<string, int>();
            foreach (var c in clocks)
                derived[$"{c}y"] = ClockPlan.EpochsFor(worldYears, c);
            manifest["epochsPerClock"] = derived;
        }
        else manifest["epochs"] = epochs;

        // variant → seed → clock → column → value
        var grid = new Dictionary<string,
            Dictionary<ulong, Dictionary<int, Dictionary<string, double>>>>();
        var failures = new List<string>();

        foreach (var (variant, knobs) in variants)
        {
            string vdir = Path.Combine(sweepDir, variant);
            Directory.CreateDirectory(vdir);
            // stamp the FULL resolved knob set as APPLIED (the artifact
            // discipline: a run's calibration is never implicit, and the
            // manifest must reflect what Set actually did, not the request)
            var resolved = new SortedDictionary<string, double>(
                StringComparer.Ordinal);
            var (probeGalaxy, probeEpoch) = Configure(
                seeds.Count > 0 ? seeds[0] : 0, epochs, knobs);
            foreach (var k in KnobRegistry.All)
                resolved[k.Name] = k.Get(probeEpoch);
            foreach (var gk in GalaxyKnobRegistry.All)
                resolved[gk.Name] = gk.Get(probeGalaxy);
            ((Dictionary<string, object?>)manifest["variants"]!)[variant]
                = resolved;

            grid[variant] = new Dictionary<ulong,
                Dictionary<int, Dictionary<string, double>>>();

            // depth mode keeps its historical layout (variant/<seed>.csv);
            // clock mode nests a directory per clock
            var lanes = clockMode ? clocks : new List<int> { 0 };
            foreach (int clock in lanes)
            {
                string dir = clockMode ? Path.Combine(vdir, Yr(clock)) : vdir;
                Directory.CreateDirectory(dir);
                foreach (var seed in seeds)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var (gconfig, econfig) = Configure(seed, epochs, knobs);
                    gconfig.GalaxyRadiusCells = radius;
                    if (clockMode)
                        // AFTER the knobs: the clock is identity, not
                        // calibration, and nothing in a variant may move it
                        ClockPlan.Apply(econfig, worldYears, clock);
                    var skeleton = SkeletonBuilder.Build(gconfig);
                    var state = EpochGenesis.Seed(skeleton, econfig);
                    new EpochEngine().Run(state);
                    sw.Stop();

                    if (clockMode)
                    {
                        // SELF-CHECK, the one that would have caught every
                        // throwaway harness: did this run actually integrate
                        // the span it claims? A ratio across mismatched spans
                        // is the failure mode this whole instrument exists to
                        // rule out, so it is asserted per run, not assumed.
                        if (state.WorldYear != worldYears)
                            failures.Add(Invariant(
                                $"{variant}/{clock}y/{seed}: integrated {state.WorldYear} world-years, not {worldYears}"));
                        if (!grid[variant].TryGetValue(seed, out var byClock))
                            grid[variant][seed] = byClock
                                = new Dictionary<int, Dictionary<string, double>>();
                        byClock[clock] = Collect(state.Health);
                    }

                    File.WriteAllText(Path.Combine(dir, $"{seed}.csv"),
                        MetricCsv.RenderMetrics(state.Health));
                    File.WriteAllText(Path.Combine(dir, $"{seed}.polities.csv"),
                        MetricCsv.RenderPolities(state.Health));
                    File.WriteAllText(Path.Combine(dir, $"{seed}.phases.csv"),
                        MetricCsv.RenderPhases(state.Health));
                    Console.WriteLine(
                        (clockMode ? $"{variant}/{clock}y/{seed}: " : $"{variant}/{seed}: ")
                        + $"{state.EpochIndex} epochs in {sw.ElapsedMilliseconds} ms");
                }
            }
        }

        if (clockMode && selfCheck)
        {
            // THE NULL VARIANT: re-run the coarsest clock and demand the same
            // numbers. Every reported ratio for it must be exactly 1.0. If the
            // instrument cannot report 1.0× when nothing changed, no other
            // number it prints means anything (and P6 byte-identity is the
            // same claim from the other side).
            int nullClock = clocks[0];
            foreach (var seed in seeds)
            {
                var (gconfig, econfig) = Configure(seed, epochs, variants[0].Knobs);
                gconfig.GalaxyRadiusCells = radius;
                ClockPlan.Apply(econfig, worldYears, nullClock);
                var state = EpochGenesis.Seed(
                    SkeletonBuilder.Build(gconfig), econfig);
                new EpochEngine().Run(state);
                var again = Collect(state.Health);
                var first = grid["baseline"][seed][nullClock];
                foreach (var r in ClockReport)
                    if (!first[r.Column].Equals(again[r.Column]))
                        failures.Add(Invariant(
                            $"NULL VARIANT FAILED {seed} {r.Column}: {first[r.Column]:R} vs {again[r.Column]:R}"));
            }
            Console.WriteLine($"null variant: {nullClock}y vs {nullClock}y re-run, "
                + $"{seeds.Count} seeds — all ratios exactly 1.0"
                + (failures.Count == 0 ? " ✓" : " ✗"));
        }

        if (clockMode)
            File.WriteAllText(Path.Combine(sweepDir, "clock-divergence.csv"),
                RenderDivergence(grid, variants, seeds, clocks));

        File.WriteAllText(Path.Combine(sweepDir, "manifest.json"),
            JsonSerializer.Serialize(manifest,
                new JsonSerializerOptions { WriteIndented = true })
                .Replace("\r\n", "\n") + "\n");   // LF like the CSVs

        if (failures.Count > 0)
        {
            // an instrument that fails its own checks must not be read as data
            Console.Error.WriteLine(
                $"INSTRUMENT SELF-CHECK FAILED ({failures.Count}):");
            foreach (var f in failures) Console.Error.WriteLine("  " + f);
            return 1;
        }
        Console.WriteLine($"sweep '{name}' complete → {sweepDir}");
        return 0;
    }

    /// <summary>Pull every reported column out of a finished run's series.
    /// Flows integrate over rows; levels read the final row. Getting that
    /// backwards on either metric silently scales the answer by the epoch
    /// count — i.e. by the clock — which is exactly what is being measured.</summary>
    private static Dictionary<string, double> Collect(MetricSeries health)
    {
        var outp = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var r in ClockReport)
        {
            var metric = MetricRegistry.Find(r.Metric)!;
            double v = 0;
            if (r.Agg == "sum")
                foreach (var row in health.Rows) v += metric.Get(row);
            else if (health.Rows.Count > 0)
                v = metric.Get(health.Rows[health.Rows.Count - 1]);
            outp[r.Column] = v;
        }
        return outp;
    }

    /// <summary>The report: one row per variant × seed × metric, the value at
    /// every clock, and the coarse→fine ratio. Nominal and real are a column,
    /// so a reader cannot collapse them by accident.</summary>
    private static string RenderDivergence(
        Dictionary<string, Dictionary<ulong,
            Dictionary<int, Dictionary<string, double>>>> grid,
        List<(string Name, Dictionary<string, double> Knobs)> variants,
        List<ulong> seeds, List<int> clocks)
    {
        int coarse = clocks[0], fine = clocks[clocks.Count - 1];
        var sb = new StringBuilder();
        sb.Append("variant,seed,metric,frame,agg");
        foreach (var c in clocks) sb.Append(",v_").Append(Yr(c));
        sb.Append(",ratio_").Append(Yr(coarse)).Append("_to_")
          .Append(Yr(fine)).Append('\n');
        foreach (var (variant, _) in variants)          // file order (P6)
            foreach (var seed in seeds)                 // file order (P6)
                foreach (var r in ClockReport)          // table order (P6)
                {
                    sb.Append(Invariant(
                        $"{variant},{seed},{r.Column},{r.Frame},{r.Agg}"));
                    foreach (var c in clocks)
                        sb.Append(',').Append(Invariant(
                            $"{grid[variant][seed][c][r.Column]:R}"));
                    sb.Append(',').Append(Ratio(
                        grid[variant][seed][coarse][r.Column],
                        grid[variant][seed][fine][r.Column])).Append('\n');
                }
        return sb.ToString();
    }

    /// <summary>A clock's label — "25y". Invariant, so the paths and headers
    /// are byte-identical on any machine's culture.</summary>
    private static string Yr(int clock) =>
        clock.ToString(System.Globalization.CultureInfo.InvariantCulture) + "y";

    /// <summary>fine ÷ coarse, with the degenerate cases named rather than
    /// printed as a number: a dead world (0 → 0) is invariant, not 0/0.</summary>
    private static string Ratio(double coarse, double fine)
    {
        if (coarse == 0.0 && fine == 0.0) return "1";
        if (coarse == 0.0) return "inf";
        return Invariant($"{fine / coarse:R}");
    }

    private static Dictionary<string, double> ReadKnobs(
        JsonElement root, string property)
    {
        var knobs = new Dictionary<string, double>();
        if (root.TryGetProperty(property, out var el))
            foreach (var k in el.EnumerateObject())
                knobs[k.Name] = k.Value.GetDouble();
        return knobs;
    }

    private static (GalaxyConfig Galaxy, EpochSimConfig Epoch) Configure(
        ulong seed, int epochs, Dictionary<string, double> knobs)
    {
        var gconfig = new GalaxyConfig { MasterSeed = seed };
        var econfig = new EpochSimConfig { MasterSeed = seed };
        econfig.Sim.EpochCount = epochs;
        // deterministic application order (P6): name-sorted
        var names = new List<string>(knobs.Keys);
        names.Sort(StringComparer.Ordinal);
        foreach (var kn in names)
        {
            if (KnobRegistry.Find(kn) is { } ek)
                ek.Set(econfig, knobs[kn]);
            else if (GalaxyKnobRegistry.Find(kn) is { } gk)
                gk.Set(gconfig, knobs[kn]);
        }
        return (gconfig, econfig);
    }
}
