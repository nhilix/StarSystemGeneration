using System.Collections.Generic;
using StarGen.Core.Atlas;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using UnityEngine;
using UnityEngine.Rendering;
// UnityEngine.SystemInfo (device caps) shadows the read model's record
using SystemInfo = StarGen.Core.Atlas.SystemInfo;

namespace StarGen.AtlasView
{
    /// <summary>One clickable thing in the orbit view: a typed selection
    /// target at a world position, plus the tooltip's line.</summary>
    public readonly struct StagePick
    {
        public readonly SelectionKind Kind;
        public readonly int Id;
        public readonly Vector3 WorldPos;
        public readonly float Radius;
        public readonly string Label;
        /// <summary>Tie-break when picks overlap (the port ring wraps its
        /// own body): higher wins — the map's priority order, kept.</summary>
        public readonly int Priority;

        public StagePick(SelectionKind kind, int id, Vector3 worldPos,
                         float radius, string label, int priority = 0)
        {
            Kind = kind;
            Id = id;
            WorldPos = worldPos;
            Radius = radius;
            Label = label;
            Priority = priority;
        }
    }

    /// <summary>K5: the orbit-view scene fragment the hex crossfades into
    /// at System LOD (spec §zoom, diagram §7): star(s), orbit rings,
    /// bodies, the port ring, facility marks, in-flight sites — all from
    /// SystemQuery over the read model, laid out at the camera's focus
    /// hex on the galactic plane. Same selection model, same panels: the
    /// stage only publishes pickables; SelectionModel drives clicks.
    /// Fades with LodBands.StageFade while the map curves die — the
    /// crossfade is complementary by construction.</summary>
    public sealed class SystemStage : MonoBehaviour
    {
        private const float StageZ = -0.30f;   // above every map layer
        private const int RingSegments = 96;

        [SerializeField] private AtlasRoot root;

        private Material _starMaterial;    // SoftDot, additive
        private Material _bodyMaterial;    // SolidDot, alpha
        private Material _portMaterial;    // Ring, alpha
        private Material _facilityMaterial; // SquareRing, alpha
        private Material _ringMaterial;    // Sprites/Default line mesh
        private readonly List<Mesh> _meshes = new();
        private GameObject _rings, _stars, _bodies, _portMarks, _facilities;

        private HexCoordinate? _builtHex;
        private bool _dirty = true;
        private float _fade = -1f;
        private readonly List<StagePick> _pickables = new();

        /// <summary>Live = the crossfade has begun; picking consults the
        /// stage first while true.</summary>
        public bool Live => _fade > 0.001f && _builtHex != null;
        public IReadOnlyList<StagePick> Pickables => _pickables;
        public HexCoordinate? BuiltHex => _builtHex;

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
                _builtHex = null;    // next entry rebuilds at the new hex
                return;
            }
            var hex = HexGrid.WorldToHex(rig.Focus.x, rig.Focus.y);
            if (_dirty || _builtHex == null || !hex.Equals(_builtHex.Value))
            {
                Build(host.Model, host.Eye, hex);
                _dirty = false;
            }
            SetFade(fade);
        }

        /// <summary>Edit-mode entry (AtlasSmoke): build and fade by hand —
        /// Update/event wiring never ran.</summary>
        public void RenderAt(AtlasReadModel model, EyeContext eye,
                             HexCoordinate hex, float fade)
        {
            Build(model, eye, hex);
            _dirty = false;
            SetFade(fade);
        }

        // ---- layout constants (world units; one hex step ≈ 1.73) ----

        private static float RingRadius(int slot) => 0.30f + 0.115f * slot;
        private static float CompanionRingRadius(int slot) =>
            0.10f + 0.05f * slot;

        private static Color32 StarTint(string typeId) => typeId switch
        {
            "ember_dwarf" => new Color32(232, 115, 74, 255),
            "amber_dwarf" => new Color32(232, 164, 74, 255),
            "gold_main" => new Color32(232, 180, 90, 255),
            "white_blaze" => new Color32(237, 242, 255, 255),
            "blue_titan" => new Color32(127, 166, 232, 255),
            "ashen_remnant" => new Color32(154, 167, 192, 255),
            "collapsed_core" => new Color32(201, 184, 232, 255),
            _ => new Color32(240, 240, 240, 255),
        };

        private static Color32 BodyTint(BodyKind kind) => kind switch
        {
            BodyKind.RockyWorld => new Color32(201, 165, 126, 255),
            BodyKind.IceWorld => new Color32(159, 196, 217, 255),
            BodyKind.GasGiant => new Color32(127, 166, 232, 255),
            BodyKind.PlanetoidBelt => new Color32(154, 167, 192, 255),
            BodyKind.Wreckage => new Color32(168, 143, 184, 255),
            _ => new Color32(200, 200, 200, 255),
        };

        // the design mock's marks: teal-ringed port, gold facility squares
        private static readonly Color32 FacilityColor = new(0xD8, 0xB4, 0x6F, 0xFF);
        private static readonly Color32 RingColor = new(140, 160, 200, 56);
        private static readonly Color32 BeltRingColor = new(154, 167, 192, 96);

        // ---- build ----

        private void Build(AtlasReadModel model, EyeContext eye,
                           HexCoordinate hex)
        {
            EnsureScaffold();
            var info = SystemQuery.At(model, eye, hex);
            _builtHex = hex;
            _pickables.Clear();
            transform.position = AtlasGeometry.HexToWorld(hex, StageZ);

            // star positions by index; orbit positions by (star, slot)
            var starPos = new Dictionary<int, Vector3>();
            var orbitPos = new Dictionary<OrbitRef, Vector3>();
            var orbitRow = new Dictionary<OrbitRef, StageOrbitRow>();
            float outermost = 0f;

            foreach (var star in info.Stars)
            {
                if (star.CompanionSlotIndex is int c)
                {
                    float r = RingRadius(c);
                    double a = SystemQuery.OrbitAngle(hex, 0, c);
                    starPos[star.Index] = new Vector3(
                        r * (float)System.Math.Cos(a),
                        r * (float)System.Math.Sin(a), 0f);
                    outermost = Mathf.Max(outermost, r);
                }
                else
                {
                    starPos[star.Index] = Vector3.zero;
                }
            }

            foreach (var row in info.Orbits)
            {
                var at = new OrbitRef(row.StarIndex, row.SlotIndex);
                bool primary = !starPos.TryGetValue(row.StarIndex, out var c0)
                               || c0 == Vector3.zero;
                float r = primary ? RingRadius(row.SlotIndex)
                                  : CompanionRingRadius(row.SlotIndex);
                double a = SystemQuery.OrbitAngle(hex, row.StarIndex,
                                                  row.SlotIndex);
                var center = starPos.TryGetValue(row.StarIndex, out var sp)
                    ? sp : Vector3.zero;
                orbitPos[at] = center + new Vector3(
                    r * (float)System.Math.Cos(a),
                    r * (float)System.Math.Sin(a), 0f);
                orbitRow[at] = row;
                if (center == Vector3.zero)
                    outermost = Mathf.Max(outermost, r);
            }

            // the deep-space station ring: attachments with no body
            float deepRadius = outermost > 0f ? outermost + 0.28f : 0.55f;

            BuildRings(info, starPos, hex);
            BuildStars(info, starPos, hex);
            BuildBodies(info, orbitPos, hex);
            BuildMarks(info, orbitPos, orbitRow, starPos, deepRadius, hex);
        }

        private Vector3 AttachmentPos(OrbitRef at,
            Dictionary<OrbitRef, Vector3> orbitPos, float deepRadius,
            HexCoordinate hex, int spreadKey)
        {
            if (at != OrbitRef.None && orbitPos.TryGetValue(at, out var p))
                return p;
            double a = SystemQuery.OrbitAngle(hex, 997, spreadKey);
            return new Vector3(deepRadius * (float)System.Math.Cos(a),
                               deepRadius * (float)System.Math.Sin(a), 0f);
        }

        private void BuildRings(SystemInfo info,
            Dictionary<int, Vector3> starPos, HexCoordinate hex)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();

            foreach (var row in info.Orbits)
            {
                bool primary = !starPos.TryGetValue(row.StarIndex, out var c)
                               || c == Vector3.zero;
                var center = starPos.TryGetValue(row.StarIndex, out var sp)
                    ? sp : Vector3.zero;
                float r = primary ? RingRadius(row.SlotIndex)
                                  : CompanionRingRadius(row.SlotIndex);
                bool belt = row.Kind == BodyKind.PlanetoidBelt;
                AddRing(vertices, colors, triangles, center, r,
                        belt ? 0.045f : 0.008f,
                        belt ? BeltRingColor : RingColor);
            }

            SetMesh(_rings, vertices, colors, triangles);
        }

        private void AddRing(List<Vector3> vertices, List<Color32> colors,
            List<int> triangles, Vector3 center, float radius, float width,
            Color32 color)
        {
            int baseIndex = vertices.Count;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i * (2f * Mathf.PI / RingSegments);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices.Add(center + dir * (radius - width * 0.5f));
                vertices.Add(center + dir * (radius + width * 0.5f));
                colors.Add(color);
                colors.Add(color);
                if (i == RingSegments) continue;
                int v = baseIndex + i * 2;
                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v + 1); triangles.Add(v + 2); triangles.Add(v + 3);
            }
        }

        private void BuildStars(SystemInfo info,
            Dictionary<int, Vector3> starPos, HexCoordinate hex)
        {
            var quads = new List<(Vector3 Pos, float World, float Px, Color32 Tint)>();
            foreach (var star in info.Stars)
            {
                bool primary = star.CompanionSlotIndex == null;
                quads.Add((starPos[star.Index],
                           primary ? 0.42f : 0.20f,
                           primary ? 30f : 16f,
                           StarTint(star.TypeId)));
                _pickables.Add(new StagePick(SelectionKind.Hex, -1,
                    transform.position + starPos[star.Index],
                    primary ? 0.24f : 0.12f,
                    star.TypeName + " · " + star.Age.ToString().ToLowerInvariant()));
            }
            SetBillboards(_stars, quads);
        }

        private void BuildBodies(SystemInfo info,
            Dictionary<OrbitRef, Vector3> orbitPos, HexCoordinate hex)
        {
            var quads = new List<(Vector3, float, float, Color32)>();
            foreach (var row in info.Orbits)
            {
                var at = new OrbitRef(row.StarIndex, row.SlotIndex);
                var pos = orbitPos[at];
                var tint = BodyTint(row.Kind);
                if (row.Kind == BodyKind.PlanetoidBelt)
                {
                    // a belt is its ring: a few fragments ride it instead
                    // of one dot
                    for (int k = 0; k < 6; k++)
                    {
                        double a = SystemQuery.OrbitAngle(hex, row.StarIndex,
                                       row.SlotIndex) + k * (System.Math.PI / 3);
                        float r = RingRadius(row.SlotIndex);
                        var p = new Vector3(r * (float)System.Math.Cos(a),
                                            r * (float)System.Math.Sin(a), 0f);
                        quads.Add((p, 0.028f, 3.5f, tint));
                    }
                }
                else
                {
                    float world = 0.05f + 0.016f * row.Size;
                    quads.Add((pos, world, 7f, tint));
                }
                _pickables.Add(new StagePick(SelectionKind.Hex, -1,
                    transform.position + pos,
                    Mathf.Max(0.10f, 0.05f + 0.016f * row.Size),
                    BodyLabel(row)));
            }
            SetBillboards(_bodies, quads);
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

        private void BuildMarks(SystemInfo info,
            Dictionary<OrbitRef, Vector3> orbitPos,
            Dictionary<OrbitRef, StageOrbitRow> orbitRow,
            Dictionary<int, Vector3> starPos, float deepRadius,
            HexCoordinate hex)
        {
            // the port: an owner-colored ring around its body (the mock's
            // teal ring), sized past the body it wraps
            var portQuads = new List<(Vector3, float, float, Color32)>();
            if (info.PortId >= 0)
            {
                var pos = AttachmentPos(info.PortAt, orbitPos, deepRadius,
                                        hex, 0);
                float body = info.PortAt != OrbitRef.None
                             && orbitRow.TryGetValue(info.PortAt, out var row)
                    ? 0.05f + 0.016f * row.Size : 0.05f;
                portQuads.Add((pos, body * 3.2f + 0.05f, 22f,
                               PortColor(info.PortId)));
                _pickables.Add(new StagePick(SelectionKind.Port, info.PortId,
                    transform.position + pos, body * 1.8f + 0.06f,
                    DockKit.Inv($"port · tier {info.PortTier} · {info.PortOwnerName}"),
                    priority: 2));
            }
            SetBillboards(_portMarks, portQuads);

            // facilities and in-flight sites: gold square outlines spread
            // on a small ring around their attached body (sites dimmer)
            var quads = new List<(Vector3, float, float, Color32)>();
            var perAnchor = new Dictionary<OrbitRef, int>();
            void Mark(OrbitRef at, int spreadKey, Color32 tint, float world,
                      SelectionKind kind, int id, string label)
            {
                perAnchor.TryGetValue(at, out int n);
                perAnchor[at] = n + 1;
                var anchor = AttachmentPos(at, orbitPos, deepRadius, hex,
                                           spreadKey);
                double a = SystemQuery.OrbitAngle(hex, 499, spreadKey)
                           + n * 0.85;
                float orbit = 0.12f + 0.022f * n;
                var pos = anchor + new Vector3(
                    orbit * (float)System.Math.Cos(a),
                    orbit * (float)System.Math.Sin(a), 0f);
                quads.Add((pos, world, 9f, tint));
                _pickables.Add(new StagePick(kind, id,
                    transform.position + pos, 0.075f, label, priority: 1));
            }

            // every surfaced facility is commissioned — SystemQuery folds
            // groundbreaking-only rows into their sites (review finding 2)
            foreach (var f in info.Facilities)
            {
                Mark(f.At, 1 + f.Id, FacilityColor, 0.058f,
                     SelectionKind.Facility, f.Id,
                     f.TypeName.ToLowerInvariant()
                     + DockKit.Inv($" t{f.Tier} · {f.OwnerName}"));
            }
            foreach (var s in info.Sites)
            {
                var tint = FacilityColor;
                tint.a = 80;
                Mark(s.At, 100000 + s.ProjectId, tint, 0.045f,
                     SelectionKind.Project, s.ProjectId,
                     s.TypeName.ToLowerInvariant()
                     + DockKit.Inv($" · under construction ({s.Progress:0%})"));
            }
            SetBillboards(_facilities, quads);
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

        // ---- meshes / materials ----

        private void EnsureScaffold()
        {
            if (_rings != null) return;
            _ringMaterial = new Material(Shader.Find("Sprites/Default"))
            { renderQueue = 3100, hideFlags = HideFlags.HideAndDontSave };

            _starMaterial = Billboard(AtlasTextures.SoftDot,
                BlendMode.SrcAlpha, BlendMode.One, 3130, maxPx: 220f);
            _bodyMaterial = Billboard(AtlasTextures.SolidDot,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3140,
                maxPx: 64f);
            _portMaterial = Billboard(AtlasTextures.Ring,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3150,
                maxPx: 110f);
            _facilityMaterial = Billboard(AtlasTextures.SquareRing,
                BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, 3150,
                maxPx: 44f);

            _rings = Child("StageRings", _ringMaterial);
            _stars = Child("StageStars", _starMaterial);
            _bodies = Child("StageBodies", _bodyMaterial);
            _portMarks = Child("StagePort", _portMaterial);
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
            List<Color32> colors, List<int> triangles)
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
            if (_rings == null)
            {
                return;   // nothing built yet — nothing to show or hide
            }
            _rings.SetActive(on);
            _stars.SetActive(on);
            _bodies.SetActive(on);
            _portMarks.SetActive(on);
            _facilities.SetActive(on);
            if (!on) return;
            _ringMaterial.color = new Color(1f, 1f, 1f, fade);
            // additive starlight scales its emitted light with the fade
            _starMaterial.SetColor("_Tint", new Color(fade, fade, fade, fade));
            var alpha = new Color(1f, 1f, 1f, fade);
            _bodyMaterial.SetColor("_Tint", alpha);
            _portMaterial.SetColor("_Tint", alpha);
            _facilityMaterial.SetColor("_Tint", alpha);
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
                if (mesh != null) DestroyResource(mesh);
            _meshes.Clear();
            if (_starMaterial != null) DestroyResource(_starMaterial);
            if (_bodyMaterial != null) DestroyResource(_bodyMaterial);
            if (_portMaterial != null) DestroyResource(_portMaterial);
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
