using System;
using StarGen.Core.Model;
using StarGen.Core.Rng;
using StarGen.Core.Substrate;

namespace StarGen.Core.Epoch;

/// <summary>The depletable body-resource stock registry's ops
/// (body-resource-stock design). A Mine or ExcavationSite draws from a finite
/// (quantity, grade) rock rolled once at groundbreaking; extraction decrements
/// it until the body runs dry, then the facility simply produces nothing and
/// falls out of IsActive's revenue like any unprofitable asset. Skimmer/
/// AgriComplex are renewable and keep no stock here. Mirrors
/// SystemRegistry.Commit's memoize-once idiom; the roll is a stateless hash
/// keyed (hex, star, slot, channel).</summary>
public static class BodyResourceOps
{
    /// <summary>Roll a depletable body's finite stock once, the first time a
    /// Mine/ExcavationSite claims it (idempotent — a repeat call is a no-op).
    /// No-op for renewable/other types, a None body, or a null system: only
    /// Mine/ExcavationSite carry a stock. Expected quantity scales with the
    /// region's raster richness (the same Ore/Exotics score the siting used);
    /// a per-body hash gives real variance so two belts in one rich hex differ.
    /// Grade reuses Potentials.RawGrade's shape (no new grade math).</summary>
    public static void Commit(SimState state, HexCoordinate hex, BodyRef body,
                              InfraTypeId type, StarSystem? system)
    {
        if (type != InfraTypeId.Mine && type != InfraTypeId.ExcavationSite)
            return;
        if (system == null || body.IsNone) return;
        var key = (hex, body);
        if (state.BodyResources.ContainsKey(key)) return;   // memoize-once

        var eco = state.Config.Economy;
        var fields = MarketEngine.FieldsAt(state, hex);
        double richness = type == InfraTypeId.Mine
            ? Potentials.Ore(fields) : Potentials.Exotics(fields);
        double expected = eco.BodyStockOreScale * richness;
        // stateless per-body variance: RollContext keyed by hex, index encodes
        // star+slot exactly like BodyGenerator (starIndex*100 + slotIndex).
        var roll = new RollContext(state.Config.MasterSeed, hex);
        int idx = body.StarIndex * 100 + body.SlotIndex;
        double u = roll.NextDouble(RollChannel.BodyResourceStock, idx); // [0,1)
        double spread = eco.BodyStockVarianceSpread;
        double quantity = Math.Max(0.0,
            expected * (1.0 - spread + 2.0 * spread * u));
        double grade = Potentials.RawGrade(richness);
        var good = Infrastructure.Get(type).Produces[0];
        state.BodyResources[key] = new Stock(good, quantity, grade);
    }

    /// <summary>Extract up to <paramref name="rated"/> units from a body's
    /// stock, capped by what remains; decrement the registry (floored at zero,
    /// never negative) and hand back the drawn quantity and the stock's grade.
    /// A dry, absent, or renewable body draws nothing.</summary>
    public static double Extract(SimState state, HexCoordinate hex,
                                 BodyRef body, double rated, out double grade)
    {
        grade = 0.0;
        if (rated <= 0) return 0.0;
        var key = (hex, body);
        if (!state.BodyResources.TryGetValue(key, out var stock)
            || stock.Quantity <= 0) return 0.0;
        double drawn = Math.Min(rated, stock.Quantity);
        grade = stock.Grade;
        state.BodyResources[key] =
            new Stock(stock.Good, stock.Quantity - drawn, stock.Grade);
        return drawn;
    }
}
