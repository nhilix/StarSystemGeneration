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

    /// <summary>Integrates one epoch: YearsPerEpoch world-years of every rate.</summary>
    public void Step(SimState state)
    {
        foreach (var phase in _phases)
            state.Trace.Add(new PhaseTraceEntry(state.EpochIndex, phase.Name,
                                                phase.Run(state)));
        state.EpochIndex++;
        state.WorldYear += state.Config.Sim.YearsPerEpoch;
    }

    /// <summary>Steps to the configured history depth.</summary>
    public void Run(SimState state)
    {
        while (state.EpochIndex < state.Config.Sim.EpochCount)
            Step(state);
    }
}
