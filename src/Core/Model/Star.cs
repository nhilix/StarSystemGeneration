using System.Collections.Generic;

namespace StarGen.Core.Model;

public sealed class Star
{
    public string TypeId { get; set; } = "";
    public string TypeName { get; set; } = "";
    public StarAge Age { get; set; }
    public List<OrbitSlot> Slots { get; } = new();
    /// <summary>Primary-star slot this companion occupies; null for the primary.</summary>
    public int? CompanionSlotIndex { get; set; }
}
