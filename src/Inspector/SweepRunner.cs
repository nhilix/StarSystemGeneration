using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;

namespace StarGen.Inspector;

/// <summary>The headless knob-sweep runner (sim-health spec §4):
/// `dotnet run --project src/Inspector -- sweep experiment.json` runs a
/// baseline and named knob-variants across a seed ensemble and writes the
/// health series as CSVs under runs/sweeps/&lt;name&gt;/. Knob names
/// resolve through the registries (epoch first, then galaxy) — unknown
/// names refuse to run, the artifact-loader contract. Output is
/// deterministic: same experiment file, byte-identical CSVs.</summary>
public static class SweepRunner
{
    public static int Run(string experimentPath)
    {
        if (!File.Exists(experimentPath))
        { Console.Error.WriteLine($"no experiment file at {experimentPath}"); return 1; }

        using var doc = JsonDocument.Parse(File.ReadAllText(experimentPath));
        var root = doc.RootElement;
        string name = root.GetProperty("name").GetString()
            ?? throw new InvalidDataException("experiment needs a name");
        var seeds = new List<ulong>();
        foreach (var s in root.GetProperty("seeds").EnumerateArray())
            seeds.Add(s.GetUInt64());
        int epochs = root.TryGetProperty("epochs", out var ep)
            ? ep.GetInt32() : new EpochSimConfig().Sim.EpochCount;
        int radius = root.TryGetProperty("radius", out var ra)
            ? ra.GetInt32() : 21;   // the REPL's default galaxy

        // variant map in file order, baseline always first
        var variants = new List<(string Name, Dictionary<string, double> Knobs)>
        { ("baseline", ReadKnobs(root, "baseline")) };
        if (root.TryGetProperty("variants", out var vs))
            foreach (var v in vs.EnumerateObject())
            {
                var merged = ReadKnobs(root, "baseline");
                foreach (var k in v.Value.EnumerateObject())
                    merged[k.Name] = k.Value.GetDouble();
                variants.Add((v.Name, merged));
            }

        // refuse unknown names before any run starts
        foreach (var (_, knobs) in variants)
            foreach (var kn in knobs.Keys)
                if (KnobRegistry.Find(kn) == null
                    && GalaxyKnobRegistry.Find(kn) == null)
                { Console.Error.WriteLine($"unknown knob '{kn}'"); return 1; }

        string sweepDir = Path.Combine("runs", "sweeps", name);
        Directory.CreateDirectory(sweepDir);
        var manifest = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["seeds"] = seeds,
            ["epochs"] = epochs,
            ["radius"] = radius,
            ["variants"] = new Dictionary<string, object?>(),
        };

        foreach (var (variant, knobs) in variants)
        {
            string dir = Path.Combine(sweepDir, variant);
            Directory.CreateDirectory(dir);
            // stamp the FULL resolved epoch-knob set (the artifact
            // discipline: a run's calibration is never implicit)
            var resolved = new SortedDictionary<string, double>(
                StringComparer.Ordinal);
            var probeConfig = Configure(seeds.Count > 0 ? seeds[0] : 0,
                epochs, knobs).Epoch;
            foreach (var k in KnobRegistry.All)
                resolved[k.Name] = k.Get(probeConfig);
            foreach (var kn in knobs.Keys)
                if (GalaxyKnobRegistry.Find(kn) is { } gk)
                    resolved[kn] = knobs[kn];
            ((Dictionary<string, object?>)manifest["variants"]!)[variant]
                = resolved;

            foreach (var seed in seeds)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var (gconfig, econfig) = Configure(seed, epochs, knobs);
                gconfig.GalaxyRadiusCells = radius;
                var skeleton = SkeletonBuilder.Build(gconfig);
                var state = EpochGenesis.Seed(skeleton, econfig);
                new EpochEngine().Run(state);
                sw.Stop();
                File.WriteAllText(Path.Combine(dir, $"{seed}.csv"),
                    MetricCsv.RenderMetrics(state.Health));
                File.WriteAllText(Path.Combine(dir, $"{seed}.polities.csv"),
                    MetricCsv.RenderPolities(state.Health));
                File.WriteAllText(Path.Combine(dir, $"{seed}.phases.csv"),
                    MetricCsv.RenderPhases(state.Health));
                Console.WriteLine($"{variant}/{seed}: {state.EpochIndex} "
                    + $"epochs in {sw.ElapsedMilliseconds} ms");
            }
        }

        File.WriteAllText(Path.Combine(sweepDir, "manifest.json"),
            JsonSerializer.Serialize(manifest,
                new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"sweep '{name}' complete → {sweepDir}");
        return 0;
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
