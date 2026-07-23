using System.Collections.Generic;

namespace StarGen.Core.Atlas;

/// <summary>The currency lens — CU-3 consolidation made visible (AC3.1,
/// atlas catch-up design §3): each polity's slot carries its own
/// currency's zone tint, parallel to WarLens/TensionLens/TechLens's SlotX
/// shape. After a federation/absorption consolidates two polities onto one
/// currency id, their slots share a color automatically — a union reads as
/// one shared zone, no bespoke union-tracking needed. A retired currency's
/// slot goes untinted, so the map visibly shows the union dissolving.</summary>
public static class CurrencyLens
{
    /// <summary>Per-slot currency id, parallel to DomainLens.PolitySlots —
    /// the slot's owner polity's own currency (the same owner→currency hop
    /// <see cref="Epoch.SimState.LocalCurrencyOf"/> makes via a port). −1
    /// for a pre-genesis/dormant slot (no currency minted yet).</summary>
    public static IReadOnlyList<int> SlotCurrency(AtlasReadModel model,
        EyeContext eye, IReadOnlyList<int> slots)
    {
        var ids = new int[slots.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = model.State.PolityOf(slots[i]).CurrencyId;
        return ids;
    }

    /// <summary>The currency-mode tint for a currency id: the SAME
    /// golden-ratio hue idiom <see cref="AtlasPalette.OwnerColor"/> uses for
    /// actors, reused on the currency id as the key — so any two slots
    /// sharing a currency id automatically share a color, which is exactly
    /// how a consolidated union reads as one shared zone (no separate
    /// palette-assignment table to keep in sync). Null (absent/untinted)
    /// for the pre-genesis sentinel (id &lt; 0) or a
    /// <see cref="Epoch.Currency.Retired"/> currency — callers fall back to
    /// <see cref="AtlasPalette.Floor"/>, the "a lens has nothing to say
    /// here" base, so the zone visibly disappears from the mode.</summary>
    public static Rgba? CurrencyColor(AtlasReadModel model, int currencyId)
    {
        if (currencyId < 0) return null;
        var currency = model.State.CurrencyOf(currencyId);
        return currency.Retired ? null : AtlasPalette.OwnerColor(currencyId);
    }
}
