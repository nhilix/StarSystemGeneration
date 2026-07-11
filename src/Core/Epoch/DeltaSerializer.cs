using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StarGen.Core.Epoch;

/// <summary>The delta boundary (narrative/handoff.md §The delta boundary):
/// everything the live game mutates records against a base artifact as
/// deltas plus the continuing event log; the procedural baseline beneath
/// (genesis strata, hex tier) stays pure. A save is: GalaxyConfig + the
/// artifact + the deltas + the log's continuation — this type writes and
/// applies the "deltas + continuation" part over the artifact's own text.
///
/// Format: STARGEN-DELTA|1|&lt;fnv64 of the base text&gt;, then one DLAYER
/// section per changed layer. The events layer is append-only by design
/// (the log never closes), so its section carries only the continuation;
/// every other changed layer is replaced whole — layer-granular, grammar-
/// agnostic, and byte-exact by construction. Unchanged layers are absent,
/// which is what keeps genesis strata out of every save.</summary>
public static class DeltaSerializer
{
    private const string Header = "STARGEN-DELTA|1";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The delta taking baseText to liveText (both artifact
    /// texts from <see cref="ArtifactSerializer.ToText"/>).</summary>
    public static string Diff(string baseText, string liveText)
    {
        var baseLayers = Split(baseText);
        var sb = new StringBuilder();
        sb.Append(Header).Append('|')
          .Append(Fnv64(baseText).ToString("x16", Inv)).Append('\n');
        foreach (var (header, lines) in SplitOrdered(liveText))
        {
            string name = LayerName(header);
            baseLayers.TryGetValue(name, out var baseLayer);
            if (baseLayer.Header == header && SameLines(baseLayer.Lines, lines))
                continue;                                 // untouched layer
            if (name == "events" && baseLayer.Header == header
                && IsPrefix(baseLayer.Lines, lines))
            {
                // the log never closes: ship only the continuation
                sb.Append("DLAYER|events|append\n");
                for (int i = baseLayer.Lines.Count; i < lines.Count; i++)
                    sb.Append(lines[i]).Append('\n');
                continue;
            }
            sb.Append("DLAYER|").Append(name).Append("|replace\n");
            sb.Append(header).Append('\n');
            foreach (var line in lines) sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Reconstructs the live artifact text from the base text and
    /// a delta produced by <see cref="Diff"/>. Byte-exact.</summary>
    public static string Apply(string baseText, string deltaText)
    {
        var deltaLines = deltaText.Split('\n');
        if (deltaLines.Length == 0
            || !deltaLines[0].StartsWith(Header + "|", StringComparison.Ordinal))
            throw new InvalidDataException("not a STARGEN-DELTA");
        string expected = deltaLines[0].Substring(Header.Length + 1);
        if (Fnv64(baseText).ToString("x16", Inv) != expected)
            throw new InvalidDataException(
                "delta was recorded against a different base artifact");

        // parse the delta's sections
        var replaced = new Dictionary<string, List<string>>();
        List<string>? appended = null;
        List<string>? current = null;
        for (int i = 1; i < deltaLines.Length; i++)
        {
            var line = deltaLines[i];
            if (i == deltaLines.Length - 1 && line.Length == 0) break;
            if (line.StartsWith("DLAYER|", StringComparison.Ordinal))
            {
                var f = line.Split('|');
                if (f.Length != 3)
                    throw new InvalidDataException($"malformed '{line}'");
                current = new List<string>();
                if (f[2] == "append")
                {
                    if (f[1] != "events")
                        throw new InvalidDataException(
                            "only the events layer appends");
                    appended = current;
                }
                else replaced[f[1]] = current;
            }
            else if (current == null)
                throw new InvalidDataException("delta line outside a DLAYER");
            else current.Add(line);
        }

        // reassemble: base layers in base order, patched where the delta says
        var sb = new StringBuilder();
        sb.Append(FirstLine(baseText)).Append('\n');
        foreach (var (header, lines) in SplitOrdered(baseText))
        {
            string name = LayerName(header);
            if (replaced.TryGetValue(name, out var replacement))
            {
                foreach (var line in replacement) sb.Append(line).Append('\n');
                continue;
            }
            sb.Append(header).Append('\n');
            foreach (var line in lines) sb.Append(line).Append('\n');
            if (name == "events" && appended != null)
                foreach (var line in appended) sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    // ---- artifact text carving ----

    private static string FirstLine(string text)
    {
        int nl = text.IndexOf('\n');
        return nl < 0 ? text : text.Substring(0, nl);
    }

    private static string LayerName(string header) => header.Split('|')[1];

    /// <summary>Layers in artifact order: (LAYER header line, body lines).</summary>
    private static List<(string Header, List<string> Lines)> SplitOrdered(
        string text)
    {
        var layers = new List<(string, List<string>)>();
        List<string>? current = null;
        var lines = text.Split('\n');
        for (int i = 1; i < lines.Length; i++)            // [0] is the header
        {
            var line = lines[i];
            if (i == lines.Length - 1 && line.Length == 0) break;
            if (line.StartsWith("LAYER|", StringComparison.Ordinal))
            {
                current = new List<string>();
                layers.Add((line, current));
            }
            else if (current == null)
                throw new InvalidDataException("artifact line outside a LAYER");
            else current.Add(line);
        }
        return layers;
    }

    private static Dictionary<string, (string Header, List<string> Lines)>
        Split(string text)
    {
        var map = new Dictionary<string, (string, List<string>)>();
        foreach (var (header, lines) in SplitOrdered(text))
            map[LayerName(header)] = (header, lines);
        return map;
    }

    private static bool SameLines(List<string>? a, List<string> b)
    {
        if (a == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    private static bool IsPrefix(List<string>? prefix, List<string> full)
    {
        if (prefix == null || prefix.Count > full.Count) return false;
        for (int i = 0; i < prefix.Count; i++)
            if (!string.Equals(prefix[i], full[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    /// <summary>FNV-1a 64 over the base text — base-mismatch detection,
    /// not security.</summary>
    private static ulong Fnv64(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text)
        {
            hash ^= (byte)c;
            hash *= 1099511628211UL;
            hash ^= (byte)(c >> 8);
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
