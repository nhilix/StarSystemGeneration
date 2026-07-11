using System.Collections.Generic;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;

namespace StarGen.Core.Atlas;

/// <summary>The single query surface the atlas presentation consumes
/// (unity-atlas-design.md architecture layer 2). Wraps a loaded SimState
/// read-only; every lens and panel query goes through here so the Unity
/// side never touches registries directly. Plain C#, zero Unity types,
/// xUnit-coverable.</summary>
public sealed class AtlasReadModel
{
    public SimState State { get; }
    public GalaxySkeleton Skeleton => State.Skeleton;
    /// <summary>The natural raster in skeleton (spiral) order — the
    /// deterministic parallel-list backbone every cell-shading lens
    /// returns colors against.</summary>
    public IReadOnlyList<RegionCell> Cells => State.Skeleton.Cells;

    public AtlasReadModel(SimState state)
    {
        State = state;
    }
}
