using StarGen.Core.Generation;
using StarGen.Core.Model;
using StarGen.Core.Naming;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>SystemFormatter's content as structured UI elements (atlas spec §3).</summary>
    public static class SystemPanelBuilder
    {
        private static readonly Color Text = new(0.85f, 0.85f, 0.88f);
        private static readonly Color Dim = new(0.55f, 0.55f, 0.62f);
        private static readonly Color Accent = new(1.0f, 0.75f, 0.31f);

        public static VisualElement Build(HexResult result, double density = double.NaN)
        {
            var scroll = new ScrollView();
            var root = scroll.contentContainer;

            if (result.System == null)
            {
                root.Add(Line(Designation.For(result.Coordinate), 16, Accent, bold: true));
                root.Add(Line("no system", 13, Dim));
                if (!double.IsNaN(density)) root.Add(Line($"density {density:F2}", 12, Dim));
                return scroll;
            }

            var system = result.System;
            root.Add(Line(system.GivenName ?? system.Designation, 16, Accent, bold: true));
            root.Add(Line($"{system.Designation} · {system.Arrangement.ToString().ToLowerInvariant()}"
                + (system.OverlayId != null ? $" · overlay: {system.OverlayId}" : ""), 12, Dim));
            foreach (var tag in system.Tags) root.Add(Line($"! {tag}", 12, Accent));

            for (int i = 0; i < system.Stars.Count; i++)
            {
                var star = system.Stars[i];
                string companion = star.CompanionSlotIndex is { } cs ? $" (slot {cs})" : "";
                root.Add(Line($"Star {(char)('A' + i)} — {star.TypeName}, "
                    + star.Age.ToString().ToLowerInvariant() + companion, 14, Text, bold: true));
                foreach (var slot in star.Slots)
                    AddSlot(root, slot);
            }
            return scroll;
        }

        private static void AddSlot(VisualElement root, OrbitSlot slot)
        {
            string band = slot.Band.ToString().ToLowerInvariant();
            if (slot.Body == null)
            {
                root.Add(Line($"  {slot.Index} [{band}] —", 12, Dim));
                return;
            }
            AddBody(root, slot.Body, $"  {slot.Index} [{band}] ");
            for (int m = 0; m < slot.Body.Satellites.Count; m++)
                AddBody(root, slot.Body.Satellites[m], $"      moon {(char)('a' + m)}: ");
        }

        private static void AddBody(VisualElement root, Body body, string prefix)
        {
            string kind = body.Kind switch
            {
                BodyKind.RockyWorld => "rocky world",
                BodyKind.IceWorld => "ice world",
                BodyKind.GasGiant => "gas giant",
                BodyKind.PlanetoidBelt => "planetoid belt",
                _ => "wreckage field",
            };
            string text = prefix + kind
                + (body.Name != null ? $" \"{body.Name}\"" : "")
                + (body.Size > 0 ? $" · size {body.Size}" : "");
            if (body.Kind == BodyKind.RockyWorld || body.Kind == BodyKind.IceWorld)
            {
                text += $" · {body.Atmosphere.ToString().ToLowerInvariant()}";
                if (body.Hydrographics > 0) text += $" · oceans {body.Hydrographics}%";
                if (body.Biosphere != Biosphere.Barren)
                    text += $" · {body.Biosphere.ToString().ToLowerInvariant()}";
            }
            root.Add(Line(text, 12, Text));
            if (body.Society is { } society)
                root.Add(Line($"        {body.Settlement.ToString().ToLowerInvariant()}"
                    + $" · pop tier {society.PopulationTier} · {society.Government}"
                    + $" · {society.Order.ToString().ToLowerInvariant()}"
                    + $" · {society.Port.ToString().ToLowerInvariant()} port", 12, Accent));
            foreach (var tag in body.Tags)
                root.Add(Line($"        POI: {tag}", 12, Accent));
        }

        private static Label Line(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }
    }
}
