namespace StarGen.Core.Epoch;

/// <summary>One phase of the generational step. Run returns the trace note
/// the engine records — deterministic text only.</summary>
public interface ISimPhase
{
    string Name { get; }
    string Run(SimState state);
}

/// <summary>The seven-phase generational step (frame/simulation-flow.md):
/// Perception → Markets → Allocation → Intent → Resolution → Interior →
/// Chronicle. One controller touchpoint (Intent); decisions run on
/// perception, consequences on truth. Slice A ships the frame — most phase
/// bodies are empty; later slices fill them without reordering.</summary>
public sealed class EpochEngine
{
    private readonly ISimPhase[] _phases =
    {
        new PerceptionPhase(),
        new MarketsPhase(),
        new AllocationPhase(),
        new IntentPhase(),
        new ResolutionPhase(),
        new InteriorPhase(),
        new ChroniclePhase(),
    };

    /// <summary>An OPTIONAL passive tap on shipment launches (AC2.F2
    /// recent flows), threaded onto the state for each step's duration and
    /// reset in a finally. Null — the default, the whole golden pipeline —
    /// changes nothing: dispatch behaves bit-identically.</summary>
    public ShipmentObserver? ShipmentObserver { get; set; }

    /// <summary>Integrates one epoch: YearsPerEpoch world-years of every rate.</summary>
    public void Step(SimState state)
    {
        state.ShipmentObserver = ShipmentObserver;
        try
        {
            // FX rates recompute once, at the very start of the epoch, from the
            // state left at the END of the prior epoch — BEFORE Markets clears and
            // rebuilds Receipts and before any conversion (Borrow/ServiceLoans/
            // PayTribute/market fills) reads the table, so the whole epoch converts
            // against one frozen table (currency-and-FX design, "FX rate").
            FxOps.RecomputeRates(state);
            foreach (var phase in _phases)
            {
                state.Trace.Add(new PhaseTraceEntry(state.EpochIndex, phase.Name,
                                                    phase.Run(state)));
                // the always-on probe: read-only, so it can never perturb —
                // the per-phase money rows attribute treasury motion
                state.Health.MoneyRows.Add(MetricsOps.Money(state, phase.Name));
            }
            // the epoch's money has all moved: write each currency's ending supply
            // back onto the live record so NEXT epoch's FxOps.RecomputeRates reads a
            // fresh, diverging value (currency-and-FX design, slice CU-1 task 9) —
            // and so this epoch's Snapshot can measure the per-currency residual
            // against it
            SupplyOps.Recompute(state);
            state.Health.Rows.Add(MetricsOps.Snapshot(state));
            state.Health.PolityRows.AddRange(MetricsOps.PolityRows(state));
            state.EpochIndex++;
            state.WorldYear += state.Config.Sim.YearsPerEpoch;
        }
        finally
        {
            // reset-safe: the tap never outlives the step it was set for
            state.ShipmentObserver = null;
        }
    }

    /// <summary>Steps to the configured history depth.</summary>
    public void Run(SimState state)
    {
        while (state.EpochIndex < state.Config.Sim.EpochCount)
            Step(state);
    }
}
