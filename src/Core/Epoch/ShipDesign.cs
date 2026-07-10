using StarGen.Core.Galaxy;
using StarGen.Core.Model;

namespace StarGen.Core.Epoch;

/// <summary>One polity's design at one chassis cell — a lineage entry
/// (fleets/ships-and-fleets.md): grid cell + the derivation inputs the stat
/// sheet recomputes from on demand (sheets are never stored). Marks drift
/// over epochs with inherited names, so a fleet's composition reads as
/// cultural history. Registry in SimState.Designs, id order fixed (P6);
/// records are immutable — a new mark appends, never edits.</summary>
public sealed class ShipDesign
{
    public int Id { get; }
    public int OwnerActorId { get; }
    public ShipRole Role { get; }
    public ShipSize Size { get; }
    /// <summary>Lineage mark, 1-based — improved marks inherit the name.</summary>
    public int Mark { get; }
    /// <summary>The inherited lineage name (the chassis cell's hull name
    /// today; renamed lineages become possible later). Views render
    /// "{Name} Mk {Mark}".</summary>
    public string Name { get; }
    /// <summary>Component grade the design was laid down at — a sheet
    /// derivation input (grade acts per-stat through the catalog's
    /// sensitivities).</summary>
    public double ComponentGrade { get; }
    /// <summary>Producer tech tier at design time — a sheet derivation input.</summary>
    public int TechTier { get; }
    public int DesignedYear { get; }

    public ShipDesign(int id, int ownerActorId, ShipRole role, ShipSize size,
                      int mark, string name, double componentGrade, int techTier,
                      int designedYear)
    {
        Id = id;
        OwnerActorId = ownerActorId;
        Role = role;
        Size = size;
        Mark = mark;
        Name = name;
        ComponentGrade = componentGrade;
        TechTier = techTier;
        DesignedYear = designedYear;
    }
}

/// <summary>Design-registry operations: registration, lineage drift, and the
/// state-aware sheet lookup (species temperament and embodiment come from the
/// owner's polity record).</summary>
public static class DesignRegistry
{
    /// <summary>Append a new design record. Mark 1 opens a lineage; higher
    /// marks inherit the lineage name.</summary>
    public static ShipDesign Register(SimState state, int ownerActorId,
                                      ShipRole role, ShipSize size, double grade,
                                      int mark = 1, string? name = null)
    {
        var design = new ShipDesign(state.Designs.Count, ownerActorId, role, size,
            mark, name ?? ShipCatalog.CellName(role, size), grade,
            state.Config.Economy.TechTierStub, state.WorldYear);
        state.Designs.Add(design);
        return design;
    }

    /// <summary>The current (highest-mark) design of one polity at one
    /// chassis cell, or null if the lineage was never opened.</summary>
    public static ShipDesign? Current(SimState state, int ownerActorId,
                                      ShipRole role, ShipSize size)
    {
        ShipDesign? best = null;
        foreach (var d in state.Designs)                  // id order (P6)
            if (d.OwnerActorId == ownerActorId && d.Role == role && d.Size == size
                && (best == null || d.Mark > best.Mark))
                best = d;
        return best;
    }

    /// <summary>Lineage drift: when the components at hand out-grade the
    /// design by the mark step, the yard lays down an improved mark — same
    /// name, mark + 1, the new grade baked in. Chronicles a ship-class
    /// launch (the military event block's first entry). Returns the design
    /// to build from.</summary>
    public static ShipDesign MaybeAdvanceMark(SimState state, ShipDesign design,
                                              double gradeAvailable,
                                              HexCoordinate at)
    {
        if (gradeAvailable < design.ComponentGrade
                             + state.Config.Fleet.MarkGradeStep)
            return design;
        var next = Register(state, design.OwnerActorId, design.Role, design.Size,
                            gradeAvailable, design.Mark + 1, design.Name);
        state.Staged.Add(new StagedEvent(
            ClockStratum.Generational, WorldEventType.ShipClassLaunched,
            new[] { design.OwnerActorId }, at, Magnitude: next.Mark,
            Valence: 0.5, EventVisibility.Public,
            new ShipClassLaunchedPayload(next.Id, next.Name, next.Mark)));
        return next;
    }

    /// <summary>The design's stat sheet, derived on demand from the owner's
    /// species (embodiment + temperament) and the design's frozen grade and
    /// tech inputs. Shape-only skeletons read as the terran default at
    /// neutral temperament.</summary>
    public static DesignSheet SheetOf(SimState state, ShipDesign design)
    {
        int sp = state.PolityOf(design.OwnerActorId).SpeciesId;
        SpeciesProfile? species = sp >= 0 && sp < state.Skeleton.Species.Count
            ? state.Skeleton.Species[sp] : null;
        return DesignMath.Sheet(design.Role, design.Size,
            species?.Embodiment ?? Embodiment.TerranAnalog,
            species?.Militancy ?? 0.5, species?.Openness ?? 0.5,
            design.TechTier, design.ComponentGrade);
    }

    /// <summary>The founding design set a polity enters with: everyone
    /// hauls, settles, and scouts; militant species arrive armed (militancy
    /// past the reserve gate: escorts; past 0.5: a line destroyer). Genesis
    /// furniture — no events, like the starter industry.</summary>
    public static void RegisterEntryDesigns(SimState state, int ownerActorId,
                                            double militancy)
    {
        const double entryGrade = 0.5;   // standard-issue starter components
        Register(state, ownerActorId, ShipRole.Freight, ShipSize.Medium, entryGrade);
        Register(state, ownerActorId, ShipRole.Colony, ShipSize.Medium, entryGrade);
        Register(state, ownerActorId, ShipRole.Scout, ShipSize.Light, entryGrade);
        if (militancy > state.Config.Controller.MilitancyReserveGate)
            Register(state, ownerActorId, ShipRole.Escort, ShipSize.Light, entryGrade);
        if (militancy > 0.5)
            Register(state, ownerActorId, ShipRole.Line, ShipSize.Medium, entryGrade);
    }
}
