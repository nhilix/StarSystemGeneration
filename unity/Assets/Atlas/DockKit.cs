using System;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>Panel content primitives (K3): the cassette vocabulary —
    /// key/value rows, section headers, meter bars, clickable rows, tags.
    /// Structure classes only; palette rides the theme.</summary>
    public static class DockKit
    {
        public static Label Sect(VisualElement into, string title)
        {
            var label = new Label(title.ToUpperInvariant());
            label.AddToClassList("ssg-sect");
            into.Add(label);
            return label;
        }

        public static void Kv(VisualElement into, string key, string value,
                              string mod = null)
        {
            var row = new VisualElement();
            row.AddToClassList("ssg-kv");
            var k = new Label(key);
            k.AddToClassList("ssg-kv__k");
            var v = new Label(value);
            v.AddToClassList("ssg-kv__v");
            if (mod != null) v.AddToClassList("ssg-kv__v--" + mod);
            row.Add(k);
            row.Add(v);
            into.Add(row);
        }

        public static Label Line(VisualElement into, string text,
                                 bool dim = false)
        {
            var label = new Label(text);
            label.AddToClassList("ssg-line");
            if (dim) label.AddToClassList("ssg-line--dim");
            into.Add(label);
            return label;
        }

        /// <summary>A meter: label, [0,1] fill, numeric readout.</summary>
        public static void Meter(VisualElement into, string label,
                                 double value, string readout = null,
                                 string mod = null)
        {
            var row = new VisualElement();
            row.AddToClassList("ssg-meter");
            var name = new Label(label);
            name.AddToClassList("ssg-meter__label");
            row.Add(name);
            var track = new VisualElement();
            track.AddToClassList("ssg-meter__track");
            var fill = new VisualElement();
            fill.AddToClassList("ssg-meter__fill");
            if (mod != null) fill.AddToClassList("ssg-meter__fill--" + mod);
            fill.style.width = Length.Percent(
                (float)(Math.Clamp(value, 0, 1) * 100));
            track.Add(fill);
            row.Add(track);
            var num = new Label(readout ?? value.ToString("0.00",
                System.Globalization.CultureInfo.InvariantCulture));
            num.AddToClassList("ssg-meter__value");
            row.Add(num);
            into.Add(row);
        }

        /// <summary>A clickable row — the block-cursor inversion idiom.</summary>
        public static Button Row(VisualElement into, Action onClick)
        {
            var row = new Button { text = string.Empty };
            row.AddToClassList("ssg-row");
            if (onClick != null) row.clicked += onClick;
            into.Add(row);
            return row;
        }

        public static Label Tag(VisualElement into, string text,
                                string mod = null)
        {
            var tag = new Label(text);
            tag.AddToClassList("ssg-tag");
            if (mod != null) tag.AddToClassList("ssg-tag--" + mod);
            into.Add(tag);
            return tag;
        }

        /// <summary>A small accent link-button inline.</summary>
        public static Button Link(VisualElement into, string text,
                                  Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("ssg-btn");
            btn.AddToClassList("ssg-btn--accent");
            into.Add(btn);
            return btn;
        }

        public static string Inv(FormattableString text) =>
            FormattableString.Invariant(text);
    }
}
