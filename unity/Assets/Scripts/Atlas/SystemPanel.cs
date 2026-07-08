using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.Atlas
{
    /// <summary>Panel root plus BodyRef→row mapping so the orbit diagram can
    /// highlight the line it just picked (orbit-diagram spec §6).</summary>
    public sealed class SystemPanel
    {
        public static readonly Color HighlightBg = new(1.0f, 0.75f, 0.31f, 0.18f);

        private readonly ScrollView _scroll;
        private readonly Dictionary<BodyRef, VisualElement> _rows = new();
        private BodyRef? _highlighted;

        public VisualElement Root => _scroll;

        internal SystemPanel(ScrollView scroll) => _scroll = scroll;

        internal void Register(BodyRef key, VisualElement row) => _rows[key] = row;

        public bool HasRow(BodyRef key) => _rows.ContainsKey(key);

        public void Highlight(BodyRef? key)
        {
            if (_highlighted is { } previous && _rows.TryGetValue(previous, out var previousRow))
                previousRow.style.backgroundColor = new StyleColor(Color.clear);
            _highlighted = null;
            if (key is { } next && _rows.TryGetValue(next, out var row))
            {
                row.style.backgroundColor = new StyleColor(HighlightBg);
                // ScrollTo needs a live layout pass; headless edit-mode panels
                // have none, so scrolling is runtime-only.
                if (_scroll.panel != null) _scroll.ScrollTo(row);
                _highlighted = next;
            }
        }
    }
}
