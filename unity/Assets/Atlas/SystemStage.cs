using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;
// UnityEngine.SystemInfo (device caps) shadows the read model's record
using SystemInfo = StarGen.Core.Atlas.SystemInfo;
using OrbitRef = StarGen.Core.Epoch.BodyRef;

namespace StarGen.AtlasView
{
    /// <summary>One clickable thing in the orbit view: a typed selection
    /// target at a world position, its subject hex, and the tooltip line.</summary>
    public readonly struct StagePick
    {
        public readonly SelectionKind Kind;
        public readonly int Id;
        public readonly HexCoordinate Hex;
        public readonly Vector3 WorldPos;
        public readonly float Radius;
        public readonly string Label;
        /// <summary>Tie-break when picks overlap (the port ring wraps its
        /// own body): higher wins — the map's priority order, kept.</summary>
        public readonly int Priority;

        public StagePick(SelectionKind kind, int id, HexCoordinate hex,
                         Vector3 worldPos, float radius, string label,
                         int priority = 0)
        {
            Kind = kind;
            Id = id;
            Hex = hex;
            WorldPos = worldPos;
            Radius = radius;
            Label = label;
            Priority = priority;
        }
    }

    /// <summary>K5: the orbit views the map crossfades into at System LOD
    /// (spec §zoom; the option-A orbit diagram is the drawing grammar:
    /// thin dark rings for EVERY slot, dashed belt rings, a tinted
    /// habitable-band annulus, star core + halo, moons, settled worlds
    /// ringed in accent). Every visible system hex renders at once — no
    /// pop-in; zooming magnifies one until it fills the view. Each
    /// system's layout is scaled to fit inside its own hex. Same
    /// selection model, same panels: the stage only publishes pickables.</summary>
    public sealed class SystemStage : MonoBehaviour
    {
        /// <summary>The plane every stage mark sits on — SelectionModel
        /// intersects pick rays here (marks bake this z; the component's
        /// own transform stays at the world origin). Coplanar with the
        /// lattice: draw order rides renderQueue, not depth — a lifted
        /// stage parallaxes against the grid (eyeball wave 2).</summary>
        public const float StageZ = -0.02f;
        private const int RingSegments = 96;
        /// <summary>Outermost ring must stay inside the hex (inradius
        /// ≈ 0.866 world) — systems never bleed onto neighbours.</summary>
        private const float FitRadius = 0.78f;
        private const int MaxVisibleHexes = 160;

        [SerializeField] private AtlasRoot root;

        private Material _ringMaterial;     // Sprites/Default line mesh
        private Material _glowMaterial;     // SoftDot, additive (halos)
        private Material _dotMaterial;      // SolidDot, alpha (bodies, cores)
        private Material _markMaterial;     // Ring texture (port/settled)
        private Material _facilityMaterial; // SquareRing
        private readonly List<Mesh> _meshes = new();
        private GameObject _rings, _glows, _dots, _marks, _facilities;

        private bool _built;
        private ulong _builtKey;
        private bool _dirty = true;
        private float _fade = -1f;
        private readonly List<HexCoordinate> _visible = new();
        private readonly List<StagePick> _pickables = new();

        // accumulation buffers, filled per build across all visible hexes
        private readonly List<Vector3> _ringVerts = new();
        private readonly List<Color> _ringColors = new();
        private readonly List<int> _ringTris = new();
        private readonly List<(Vector3 Pos, float World, float Px, Color32 Tint)>
            _glowQuads = new(), _dotQuads = new(), _markQuads = new(),
            _facilityQuads = new();

        /// <summary>Live = the crossfade has begun; picking consults the
        /// stage first while true.</summary>
        public bool Live => _fade > 0.001f && _built;
        public IReadOnlyList<StagePick> Pickables => _pickables;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private void OnEnable()
        {
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded += MarkDirty;
                root.SimHost.TimeChanged += MarkDirty;
            }
        }

        private void OnDisable()
        {
            if (root != null && root.SimHost != null)
            {
                root.SimHost.Loaded -= MarkDirty;
                root.SimHost.TimeChanged -= MarkDirty;
            }
        }

        private void MarkDirty() => _dirty = true;

        private void Update()
        {
            if (root == null || root.CameraRig == null) return;
            var host = root.SimHost;
            if (host == null || host.Model == null)
            {
                SetFade(0f);
                return;
            }
            var rig = root.CameraRig;
            float fade = LodBands.StageFade(rig.Distance, rig.GalaxyExtent);
            if (fade <= 0.001f)
            {
                SetFade(0f);
                _built = false;
                _builtKey = 0;
                return;
            }
            ComputeVisibleHexes(rig);
            ulong key = VisibleKey();
            if (_dirty || !_built || key != _builtKey)
            {
                BuildAll(host.Model, host.Eye, _visible);
                _builtKey = key;
                _dirty = false;
            }
            SetFade(fade);
        }

        /// <summary>Edit-mode entry (AtlasSmoke): build one hex and fade
        /// by hand — Update/event wiring never ran.</summary>
        public void RenderAt(AtlasReadModel model, EyeContext eye,
                             HexCoordinate hex, float fade)
        {
            _visible.Clear();
            _visible.Add(hex);
            BuildAll(model, eye, _visible);
            _builtKey = VisibleKey();
            _dirty = false;
            SetFade(fade);
        }

        /// <summary>Edit-mode entry, area form: every hex within
        /// <paramref name="radius"/> of the center — the multi-system
        /// field the play-mode Update path shows.</summary>
        public void RenderArea(AtlasReadModel model, EyeContext eye,
                               HexCoordinate center, int radius, float fade)
        {
            _visible.Clear();
            for (int q = center.Q - radius; q <= center.Q + radius; q++)
                for (int r = center.R - radius; r <= center.R + radius; r++)
                {
                    var hex = new HexCoordinate(q, r);
                    if (HexGrid.Distance(center, hex) <= radius)
                        _visible.Add(hex);
                }
            BuildAll(model, eye, _visible);
            _builtKey = VisibleKey();
            _dirty = false;
            SetFade(fade);
        }

        // ---- visibility: every hex the camera can see on the plane ----

        private void ComputeVisibleHexes(CameraRig rig)
        {
            var cam = rig.Cam;
            var focus = rig.Focus;
            float maxHalf = rig.Distance * 3.0f;   // pitch stretches the
            // top edge toward the horizon — clamp the rect around focus

            float minX = focus.x, maxX = focus.x;
            float minY = focus.y, maxY = focus.y;
            for (int c = 0; c < 4; c++)
            {
                var screen = new Vector3(c % 2 == 0 ? 0 : cam.pixelWidth,
                                         c < 2 ? 0 : cam.pixelHeight, 0f);
                var ray = cam.ScreenPointToRay(screen);
                Vector3 p;
                if (Mathf.Abs(ray.direction.z) < 1e-5f) p = focus;
                else
                {
                    float t = -ray.origin.z / ray.direction.z;
                    p = t > 0f ? ray.origin + ray.direction * t : focus;
                }
                p.x = Mathf.Clamp(p.x, focus.x - maxHalf, focus.x + maxHalf);
                p.y = Mathf.Clamp(p.y, focus.y - maxHalf, focus.y + maxHalf);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            const float pad = 1.2f;   // a hex reaches ~1 world past center
            minX -= pad; maxX += pad; minY -= pad; maxY += pad;

            // axial→world is linear, so corner hex coords bound the rect
            int qMin = int.MaxValue, qMax = int.MinValue;
            int rMin = int.MaxValue, rMax = int.MinValue;
            var corners = new (float X, float Y)[]
            { (minX, minY), (maxX, minY), (minX, maxY), (maxX, maxY) };
            foreach (var (x, y) in corners)
            {
                var h = HexGrid.WorldToHex(x, y);
                qMin = Mathf.Min(qMin, h.Q); qMax = Mathf.Max(qMax, h.Q);
                rMin = Mathf.Min(rMin, h.R); rMax = Mathf.Max(rMax, h.R);
            }

            _visible.Clear();
            for (int q = qMin - 1; q <= qMax + 1; q++)
                for (int r = rMin - 1; r <= rMax + 1; r++)
                {
                    var hex = new HexCoordinate(q, r);
                    var (wx, wy) = HexGrid.HexToWorld(hex);
                    if (wx < minX || wx > maxX || wy < minY || wy > maxY)
                        continue;
                    _visible.Add(hex);
                    if (_visible.Count >= MaxVisibleHexes) return;
                }
        }

        private ulong VisibleKey()
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (var hex in _visible)
                {
                    h = (h ^ (ulong)(uint)hex.Q) * 1099511628211UL;
                    h = (h ^ (ulong)(uint)hex.R) * 1099511628211UL;
                }
                return h == 0 ? 1UL : h;
            }
        }

        // ---- layout (raw radii; the fit scale shrinks per system) ----

        private static float RingRadius(int slot) => 0.30f + 0.115f * slot;
        private static float CompanionRingRadius(int slot) =>
            0.10f + 0.05f * slot;

        // ---- the option-A palette ----

        private static Color32 StarTint(string typeId) => typeId switch
        {
            "ember_dwarf" => new Color32(0xFF, 0x8A, 0x5C, 0xFF),
            "amber_dwarf" => new Color32(0xFF, 0xC0, 0x66, 0xFF),
            "gold_main" => new Color32(0xFF, 0xD0, 0x66, 0xFF),
            "white_blaze" => new Color32(0xED, 0xF2, 0xFF, 0xFF),
            "blue_titan" => new Color32(0x7F, 0xA6, 0xE8, 0xFF),
            "ashen_remnant" => new Color32(0x9A, 0xA7, 0xC0, 0xFF),
            "collapsed_core" => new Color32(0xC9, 0xB8, 0xE8, 0xFF),
            _ => new Color32(240, 240, 240, 255),
        };

        private static Color32 BodyTint(BodyKind kind) => kind switch
        {
            BodyKind.RockyWorld => new Color32(0xC9, 0xA0, 0x6A, 0xFF),
            BodyKind.IceWorld => new Color32(0xA8, 0xD8, 0xE8, 0xFF),
            BodyKind.GasGiant => new Color32(0xE0, 0x88, 0x40, 0xFF),
            BodyKind.Wreckage => new Color32(0xA8, 0x8F, 0xB8, 0xFF),
            _ => new Color32(200, 200, 200, 255),
        };

        private static readonly Color32 RingColor = new(0x26, 0x2C, 0x3F, 0xFF);
        private static readonly Color32 BeltColor = new(0x9A, 0x8F, 0x7A, 0xB4);
        private static readonly Color32 HabColor = new(0x3F, 0xBF, 0x7F, 0x10);
        private static readonly Color32 MoonColor = new(0xB9, 0xBF, 0xD0, 0xFF);
        private static readonly Color32 SettledColor = new(0xFF, 0xBF, 0x4F, 0xFF);
        private static readonly Color32 FacilityColor = new(0xD8, 0xB4, 0x6F, 0xFF);

        // ---- build ----

        private void BuildAll(AtlasReadModel model, EyeContext eye,
                              List<HexCoordinate> hexes)
        {
            EnsureScaffold();
            transform.position = Vector3.zero;   // meshes are world-space
            _pickables.Clear();
            _ringVerts.Clear(); _ringColors.Clear(); _ringTris.Clear();
            _glowQuads.Clear(); _dotQuads.Clear(); _markQuads.Clear();
            _facilityQuads.Clear();

            foreach (var hex in hexes)
                BuildOne(model, eye, hex);

            SetMesh(_rings, _ringVerts, _ringColors, _ringTris);
            SetBillboards(_glows, _glowQuads);
            SetBillboards(_dots, _dotQuads);
            SetBillboards(_marks, _markQuads);
            SetBillboards(_facilities, _facilityQuads);
            _built = true;
        }

        private void BuildOne(AtlasReadModel model, EyeContext eye,
                              HexCoordinate hex)
        {
            var info = SystemQuery.At(model, eye, hex);
            if (!info.HasSystem && info.PortId < 0
                && info.Facilities.Count == 0 && info.Sites.Count == 0)
                return;   // truly empty reach — the wilds stay dark

            var origin = AtlasGeometry.HexToWorld(hex, StageZ);

            // star centers (raw, before the fit scale) and the raw extent
            var starCenter = new Dictionary<int, Vector2>();
            var starMaxRing = new Dictionary<int, float>();
            float outermost = 0f;
            foreach (var ring in info.Rings)
            {
                float r = ring.StarIndex == 0 ? RingRadius(ring.SlotIndex)
                    : CompanionRingRadius(ring.SlotIndex);
                if (!starMaxRing.TryGetValue(ring.StarIndex, out float m)
                    || r > m)
                    starMaxRing[ring.StarIndex] = r;
            }
            foreach (var star in info.Stars)
            {
                if (star.CompanionSlotIndex is int c && star.Index != 0)
                {
                    float r = RingRadius(c);
                    double a = SystemQuery.OrbitAngle(hex, 0, c);
                    starCenter[star.Index] = new Vector2(
                        r * (float)System.Math.Cos(a),
                        r * (float)System.Math.Sin(a));
                    starMaxRing.TryGetValue(star.Index, out float sub);
                    outermost = Mathf.Max(outermost, r + sub);
                }
                else
                {
                    starCenter[star.Index] = Vector2.zero;
                    starMaxRing.TryGetValue(star.Index, out float own);
                    outermost = Mathf.Max(outermost, own);
                }
            }
            // the fit: never bleed past the hex (shrink only)
            float s = outermost > FitRadius ? FitRadius / outermost : 1f;

            // rings first: every slot, belts dashed, hab band as annulus
            var habLo = new Dictionary<int, float>();
            var habHi = new Dictionary<int, float>();
            foreach (var ring in info.Rings)
            {
                bool primary = ring.StarIndex == 0;
                float radius = (primary ? RingRadius(ring.SlotIndex)
                    : CompanionRingRadius(ring.SlotIndex)) * s;
                var center = Center(origin, starCenter, ring.StarIndex, s);
                if (ring.IsBelt)
                    AddRing(center, radius, 0.010f, BeltColor, dashed: true);
                else
                    AddRing(center, radius, 0.0045f, RingColor, dashed: false);
                if (ring.Band == OrbitBand.Habitable)
                {
                    int k = ring.StarIndex;
                    if (!habLo.TryGetValue(k, out float lo) || radius < lo)
                        habLo[k] = radius;
                    if (!habHi.TryGetValue(k, out float hi) || radius > hi)
                        habHi[k] = radius;
                }
            }
            foreach (var pair in habLo)
            {
                // the band sits INSIDE the gap between its rings — at hex
                // scale the mock's full-gap stroke reads heavy, so inset
                float spacing = (pair.Key == 0 ? 0.115f : 0.05f) * s;
                float lo = pair.Value + spacing * 0.15f;
                float hi = habHi[pair.Key] - spacing * 0.15f;
                if (hi - lo < spacing * 0.4f)
                {
                    float mid = (pair.Value + habHi[pair.Key]) * 0.5f;
                    lo = mid - spacing * 0.2f;
                    hi = mid + spacing * 0.2f;
                }
                AddRing(Center(origin, starCenter, pair.Key, s),
                        (lo + hi) * 0.5f, hi - lo, HabColor, dashed: false);
            }

            // stars: additive halo + solid core
            foreach (var star in info.Stars)
            {
                bool primary = star.CompanionSlotIndex == null;
                var tint = StarTint(star.TypeId);
                var pos = Center(origin, starCenter, star.Index, s);
                float core = (primary ? 0.085f : 0.05f) * Mathf.Max(s, 0.6f);
                var halo = tint; halo.a = 0x30;
                _glowQuads.Add((pos, core * 3.4f, primary ? 26f : 15f, halo));
                _dotQuads.Add((pos, core, primary ? 9f : 6f, tint));
                _pickables.Add(new StagePick(SelectionKind.System, -1, hex,
                    pos, Mathf.Max(core * 1.6f, 0.1f),
                    star.TypeName + " · "
                    + star.Age.ToString().ToLowerInvariant()));
            }

            // bodies + moons + the settled accent ring
            var orbitPos = new Dictionary<OrbitRef, Vector3>();
            var orbitWorld = new Dictionary<OrbitRef, float>();
            foreach (var row in info.Orbits)
            {
                var at = new OrbitRef(row.StarIndex, row.SlotIndex);
                float radius = (row.StarIndex == 0
                    ? RingRadius(row.SlotIndex)
                    : CompanionRingRadius(row.SlotIndex)) * s;
                double a = SystemQuery.OrbitAngle(hex, row.StarIndex,
                                                  row.SlotIndex);
                var center = Center(origin, starCenter, row.StarIndex, s);
                var pos = center + new Vector3(
                    radius * (float)System.Math.Cos(a),
                    radius * (float)System.Math.Sin(a), 0f);
                orbitPos[at] = pos;

                if (row.Kind == BodyKind.PlanetoidBelt)
                {
                    orbitWorld[at] = 0.02f;
                    // the belt IS its dashed ring — no body dot
                    _pickables.Add(new StagePick(SelectionKind.System, -1,
                        hex, pos, 0.08f, BodyLabel(row)));
                    continue;
                }
                float world = (0.028f + 0.009f * row.Size)
                              * Mathf.Max(s, 0.6f);
                orbitWorld[at] = world;
                _dotQuads.Add((pos, world, 5.5f, BodyTint(row.Kind)));
                if (row.Settlement != Settlement.None)
                    _markQuads.Add((pos, world * 2.9f, 13f, SettledColor));
                int moons = Mathf.Min(row.SatelliteCount, 4);
                for (int m = 0; m < moons; m++)
                {
                    // hugging the body: just off the dot's rim ('world'
                    // is the billboard's full size, so radius is half) —
                    // the old 1.8× offset scattered gas-giant moons a
                    // ring-gap away (eyeball wave 2)
                    double ma = SystemQuery.OrbitAngle(hex, 61 + m,
                                                       row.SlotIndex);
                    float mr = world * 0.68f + 0.008f;
                    _dotQuads.Add((pos + new Vector3(
                        mr * (float)System.Math.Cos(ma),
                        mr * (float)System.Math.Sin(ma), 0f),
                        0.011f, 2.2f, MoonColor));
                }
                _pickables.Add(new StagePick(SelectionKind.System, -1, hex,
                    pos, Mathf.Max(world * 1.7f, 0.07f), BodyLabel(row)));
            }

            // the deep-space station ring: attachments with no body
            float deepRadius = outermost > 0f
                ? Mathf.Min(outermost * s + 0.14f, FitRadius + 0.05f)
                : 0.42f;

            Vector3 Attach(OrbitRef at, int spreadKey)
            {
                if (at != OrbitRef.None && orbitPos.TryGetValue(at, out var p))
                    return p;
                double a = SystemQuery.OrbitAngle(hex, 997, spreadKey);
                return origin + new Vector3(
                    deepRadius * (float)System.Math.Cos(a),
                    deepRadius * (float)System.Math.Sin(a), 0f);
            }

            // the port: an owner-colored ring around its body
            if (info.PortId >= 0)
            {
                var pos = Attach(info.PortAt, 0);
                float body = info.PortAt != OrbitRef.None
                    && orbitWorld.TryGetValue(info.PortAt, out float w)
                    ? w : 0.03f;
                _markQuads.Add((pos, body * 4.2f + 0.03f, 20f,
                                PortColor(info.PortId)));
                _pickables.Add(new StagePick(SelectionKind.Port, info.PortId,
                    hex, pos, body * 2.2f + 0.05f,
                    DockKit.Inv($"port · tier {info.PortTier} · {info.PortOwnerName}"),
                    priority: 2));
            }

            // facilities and in-flight sites: gold squares off their body
            var perAnchor = new Dictionary<OrbitRef, int>();
            void Mark(OrbitRef at, int spreadKey, Color32 tint, float world,
                      SelectionKind kind, int id, string label)
            {
                perAnchor.TryGetValue(at, out int n);
                perAnchor[at] = n + 1;
                var anchor = Attach(at, spreadKey);
                float body = at != OrbitRef.None
                    && orbitWorld.TryGetValue(at, out float w) ? w : 0.02f;
                double a = SystemQuery.OrbitAngle(hex, 499, spreadKey)
                           + n * 0.85;
                float orbit = body + 0.05f + 0.016f * n;
                var pos = anchor + new Vector3(
                    orbit * (float)System.Math.Cos(a),
                    orbit * (float)System.Math.Sin(a), 0f);
                _facilityQuads.Add((pos, world, 7f, tint));
                _pickables.Add(new StagePick(kind, id, hex, pos, 0.055f,
                                             label, priority: 1));
            }

            foreach (var f in info.Facilities)
                Mark(f.At, 1 + f.Id, FacilityColor, 0.038f,
                     SelectionKind.Facility, f.Id,
                     f.TypeName.ToLowerInvariant()
                     + DockKit.Inv($" t{f.Tier} · {f.OwnerName}"));
            foreach (var site in info.Sites)
            {
                var tint = FacilityColor;
                tint.a = 80;
                Mark(site.At, 100000 + site.ProjectId, tint, 0.030f,
                     SelectionKind.Project, site.ProjectId,
                     site.TypeName.ToLowerInvariant()
                     + DockKit.Inv($" · under construction ({site.Progress:0%})"));
            }
        }

        private static Vector3 Center(Vector3 origin,
            Dictionary<int, Vector2> starCenter, int starIndex, float s)
        {
            var c = starCenter.TryGetValue(starIndex, out var sc)
                ? sc : Vector2.zero;
            return origin + new Vector3(c.x * s, c.y * s, 0f);
        }

        private static string BodyLabel(StageOrbitRow row)
        {
            string kind = row.Kind switch
            {
                BodyKind.RockyWorld => "rocky world",
                BodyKind.IceWorld => "ice world",
                BodyKind.GasGiant => "gas giant",
                BodyKind.PlanetoidBelt => "planetoid belt",
                BodyKind.Wreckage => "wreckage field",
                _ => row.Kind.ToString().ToLowerInvariant(),
            };
            string name = row.Name != null ? row.Name + " · " : "";
            string settled = row.Settlement != Settlement.None
                ? " · " + row.Settlement.ToString().ToLowerInvariant()
                : "";
            string moons = row.SatelliteCount switch
            {
                0 => "",
                1 => " · 1 moon",
                _ => DockKit.Inv($" · {row.SatelliteCount} moons"),
            };
            return name + kind + settled + moons;
        }

        private Color32 PortColor(int portId)
        {
            var markers = PortLens.Markers(root.SimHost.Model,
                                           root.SimHost.Eye);
            foreach (var m in markers)
                if (m.PortId == portId)
                    return AtlasGeometry.ToColor32(m.Color);
            return new Color32(0x46, 0xB5, 0xA4, 0xFF);
        }

        // ---- ring geometry ----

        private void AddRing(Vector3 center, float radius, float width,
                             Color32 color, bool dashed)
        {
            // vertex colors bypass Unity's sRGB handling — linearize like
            // every billboard layer does, or the palette renders washed
            // (#262C3F rings came out lavender, the 9% hab band solid)
            var linear = ((Color)color).linear;
            linear.a = color.a / 255f;   // alpha is never sRGB-encoded
            int baseIndex = _ringVerts.Count;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i * (2f * Mathf.PI / RingSegments);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                _ringVerts.Add(center + dir * (radius - width * 0.5f));
                _ringVerts.Add(center + dir * (radius + width * 0.5f));
                _ringColors.Add(linear);
                _ringColors.Add(linear);
                if (i == RingSegments) continue;
                // fine-grained dashing (2 on, 1 off of 96 segments) — the
                // belt reads as a grainy ring, not scattered ticks
                if (dashed && i % 3 == 2) continue;
                int v = baseIndex + i * 2;
                _ringTris.Add(v); _ringTris.Add(v + 2); _ringTris.Add(v + 1);
                _ringTris.Add(v + 1); _ringTris.Add(v + 2); _ringTris.Add(v + 3);
            }
        }

        // ---- meshes / materials ----

        private void EnsureScaffold()
        {
            if (_rings != null) return;
            _ringMaterial = new Material(Shader.Find("Sprites/Default"))
            { renderQueue = 3100, hideFlags = HideFlags.HideAndDontSave };

            _glowMaterial = Billboard(AtlasTextures.SoftDot,
                BlendMode.SrcAlpha, BlendMode.One, 3130, maxPx: 200f);
            _dotMaterial = Billboard(AtlasTextures.SolidDot,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3140,
                maxPx: 56f);
            _markMaterial = Billboard(AtlasTextures.ThinRing,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3150,
                maxPx: 100f);
            _facilityMaterial = Billboard(AtlasTextures.SquareRing,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3150,
                maxPx: 36f);

            _rings = Child("StageRings", _ringMaterial);
            _glows = Child("StageGlows", _glowMaterial);
            _dots = Child("StageDots", _dotMaterial);
            _marks = Child("StageMarks", _markMaterial);
            _facilities = Child("StageFacilities", _facilityMaterial);
        }

        private static Material Billboard(Texture2D texture, BlendMode src,
            BlendMode dst, int queue, float maxPx)
        {
            var m = new Material(Shader.Find("StarGen/AtlasBillboard"))
            { hideFlags = HideFlags.HideAndDontSave, renderQueue = queue };
            m.SetTexture("_MainTex", texture);
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_MaxPx", maxPx);
            return m;
        }

        private GameObject Child(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void SetMesh(GameObject go, List<Vector3> vertices,
            List<Color> colors, List<int> triangles)
        {
            var mesh = new Mesh
            { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Swap(go, mesh);
        }

        private void SetBillboards(GameObject go,
            List<(Vector3 Pos, float World, float Px, Color32 Tint)> quads)
        {
            var vertices = new Vector3[quads.Count * 4];
            var corners = new List<Vector4>(quads.Count * 4);
            var colors = new Color[quads.Count * 4];
            var triangles = new int[quads.Count * 6];
            for (int i = 0; i < quads.Count; i++)
            {
                var (pos, world, px, tint) = quads[i];
                var color = ((Color)tint).linear;
                color.a = tint.a / 255f;   // alpha is never sRGB-encoded
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    vertices[v + c] = pos;
                    colors[v + c] = color;
                }
                corners.Add(new Vector4(-0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, -0.5f, world, px));
                corners.Add(new Vector4(0.5f, 0.5f, world, px));
                corners.Add(new Vector4(-0.5f, 0.5f, world, px));
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }
            var mesh = new Mesh
            { indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, corners);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(2f);
            mesh.bounds = bounds;
            Swap(go, mesh);
        }

        private void Swap(GameObject go, Mesh mesh)
        {
            var filter = go.GetComponent<MeshFilter>();
            var old = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (old != null)
            {
                _meshes.Remove(old);
                DestroyResource(old);
            }
            _meshes.Add(mesh);
        }

        private void SetFade(float fade)
        {
            if (Mathf.Approximately(fade, _fade)) return;
            _fade = fade;
            bool on = fade > 0.001f;
            if (_rings == null) return;   // nothing built yet
            _rings.SetActive(on);
            _glows.SetActive(on);
            _dots.SetActive(on);
            _marks.SetActive(on);
            _facilities.SetActive(on);
            if (!on) return;
            _ringMaterial.color = new Color(1f, 1f, 1f, fade);
            // additive starlight scales its emitted light with the fade
            _glowMaterial.SetColor("_Tint", new Color(fade, fade, fade, fade));
            var alpha = new Color(1f, 1f, 1f, fade);
            _dotMaterial.SetColor("_Tint", alpha);
            _markMaterial.SetColor("_Tint", alpha);
            _facilityMaterial.SetColor("_Tint", alpha);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
                if (mesh != null) DestroyResource(mesh);
            _meshes.Clear();
            if (_glowMaterial != null) DestroyResource(_glowMaterial);
            if (_dotMaterial != null) DestroyResource(_dotMaterial);
            if (_markMaterial != null) DestroyResource(_markMaterial);
            if (_facilityMaterial != null) DestroyResource(_facilityMaterial);
            if (_ringMaterial != null) DestroyResource(_ringMaterial);
        }

        private static void DestroyResource(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
