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

        // ---- structured tables (AC2.F1) — market/book/contracts rows as
        // aligned columns instead of dense concatenated lines. A "table" is
        // a plain column of `.ssg-table__row`s; each row is a flex row of
        // `.ssg-table__cell`s. Width classes (w36…w84) are small reusable
        // buckets, not per-table bespoke classes — see AtlasChrome.uss.
        // Cells truncate with ellipsis by default so a long name can never
        // push a row wider than its table (no horizontal overflow).

        public static VisualElement Table(VisualElement into)
        {
            var table = new VisualElement();
            table.AddToClassList("ssg-table");
            into.Add(table);
            return table;
        }

        /// <summary>A non-interactive table row (header or plain data row).</summary>
        public static VisualElement TableRow(VisualElement table,
                                             bool head = false)
        {
            var row = new VisualElement();
            row.AddToClassList("ssg-table__row");
            if (head) row.AddToClassList("ssg-table__row--head");
            table.Add(row);
            return row;
        }

        /// <summary>A clickable table row — the same block-cursor inversion
        /// idiom as <see cref="Row"/>, applied to a table row instead of a
        /// single-line one.</summary>
        public static Button TableRowLink(VisualElement table, Action onClick)
        {
            var row = new Button { text = string.Empty };
            row.AddToClassList("ssg-table__row");
            row.AddToClassList("ssg-table__row--link");
            if (onClick != null) row.clicked += onClick;
            table.Add(row);
            return row;
        }

        /// <summary>One plain-text table cell. <paramref name="widthClass"/>
        /// is one of the ssg-table__cell--w* buckets, or null for a
        /// flexible (grow/shrink/ellipsis) column — pass "flex" for that.
        /// <paramref name="num"/> right-aligns for genuinely numeric
        /// columns (qty, price, fee…); compound text (grade band, black
        /// book) reads better left-aligned even in a numeric-leaning
        /// column.</summary>
        public static Label Cell(VisualElement row, string text,
                                 string widthClass, bool num = false,
                                 string mod = null, bool dim = false)
        {
            var cell = new Label(text);
            cell.AddToClassList("ssg-table__cell");
            if (widthClass != null)
                cell.AddToClassList("ssg-table__cell--" + widthClass);
            if (num) cell.AddToClassList("ssg-table__cell--num");
            if (dim) cell.AddToClassList("ssg-table__cell--dim");
            if (mod != null) cell.AddToClassList("ssg-table__cell--" + mod);
            row.Add(cell);
            return cell;
        }

        /// <summary>A two-line cell (main value + a dim sub-line, e.g. a
        /// route with "posted by X" beneath it) — still one table column,
        /// no extra row height beyond what the sub-line needs.</summary>
        public static VisualElement CellStack(VisualElement row,
                                              string widthClass)
        {
            var cell = new VisualElement();
            cell.AddToClassList("ssg-table__cell");
            cell.AddToClassList("ssg-table__cell--stack");
            if (widthClass != null)
                cell.AddToClassList("ssg-table__cell--" + widthClass);
            row.Add(cell);
            return cell;
        }

        public static Label CellLine(VisualElement stackCell, string text,
                                     bool dim = false)
        {
            var label = new Label(text);
            label.AddToClassList("ssg-table__line");
            if (dim) label.AddToClassList("ssg-table__line--dim");
            stackCell.Add(label);
            return label;
        }

        public static string Inv(FormattableString text) =>
            FormattableString.Invariant(text);
    }
}
