using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using StarGen.Core.Model;
using Xunit;

namespace StarGen.Core.Tests.Epoch;

/// <summary>Slice D task 6 — Interior rework
/// (polity/population-and-identity.md): growth = f(SoL, provisions access,
/// embodiment), famine shrink, machine populations gated by industry access,
/// migration along gradients over lanes (refugees flee famine, identity
/// travels), ideology drifting with lived conditions.</summary>
public class InteriorEconomyTests
{
    /// <summary>Two lane-connected ports of one entered polity, one segment
    /// each, no pending entries (EntryEpoch pushed out).</summary>
    private static (SimState State, PopulationSegment Src, PopulationSegment Dst)
        TwoPortFixture()
    {
        var state = EpochTestKit.Seeded().State;
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.TerranAnalog;
        var actor = state.Actors[0];
        actor.Entered = true;
        var pa = new Port(0, actor.Id, actor.Seat, tier: 2, foundedYear: 0);
        var pb = new Port(1, actor.Id,
            new HexCoordinate(actor.Seat.Q + 10, actor.Seat.R), tier: 2, 0);
        state.Ports.Add(pa);
        state.Ports.Add(pb);
        state.Markets.Add(new Market(0, state.Config.Economy));
        state.Markets.Add(new Market(1, state.Config.Economy));
        EpochTestKit.AddLane(state, 0, 1);
        int species = state.PolityOf(actor.Id).SpeciesId;
        var src = new PopulationSegment(0, 0, species, species, 2.0)
        { Wealth = 100 };
        var dst = new PopulationSegment(1, 1, species, species, 2.0)
        { Wealth = 100 };
        state.Segments.Add(src);
        state.Segments.Add(dst);
        state.WorldYear = 100;
        state.EpochIndex = 4;
        return (state, src, dst);
    }

    [Fact]
    public void Growth_ScalesWithSoLAndSubsistence()
    {
        var (fed, fedSeg, _) = TwoPortFixture();
        fedSeg.SoL = 0.9;
        fedSeg.LastSubsistence = 1.0;
        double fedBefore = fedSeg.Size;
        new InteriorPhase().Run(fed);
        double fedGrowth = fedSeg.Size - fedBefore;

        var (lean, leanSeg, _) = TwoPortFixture();
        leanSeg.SoL = 0.2;
        leanSeg.LastSubsistence = 0.85;   // fed enough not to shrink
        double leanBefore = leanSeg.Size;
        new InteriorPhase().Run(lean);
        double leanGrowth = leanSeg.Size - leanBefore;

        Assert.True(fedGrowth > 0);
        Assert.True(fedGrowth > leanGrowth,
            $"prosperity should outgrow poverty: {fedGrowth} vs {leanGrowth}");
    }

    [Fact]
    public void Famine_ShrinksTheSegment()
    {
        var (state, seg, _) = TwoPortFixture();
        seg.LastSubsistence = 0.3;
        double before = seg.Size;
        new InteriorPhase().Run(state);
        Assert.True(seg.Size < before, "famine shrinks segments");
    }

    [Fact]
    public void MachinePopulation_StallsWithoutIndustry()
    {
        var (state, seg, _) = TwoPortFixture();
        foreach (var sp in state.Skeleton.Species)
            sp.Embodiment = Embodiment.Machine;
        seg.SoL = 0.9;
        seg.LastSubsistence = 0.0;        // cut off from machinery and fuel
        double before = seg.Size;
        new InteriorPhase().Run(state);
        Assert.True(seg.Size <= before,
            "a machine population without industry ages out, never grows");
    }

    [Fact]
    public void Migration_FlowsTowardTheFedPort()
    {
        var (state, src, dst) = TwoPortFixture();
        src.LastSubsistence = 0.3;        // starving: refugees flee
        dst.LastSubsistence = 1.0;
        dst.SoL = 0.8;
        double srcBefore = src.Size, dstBefore = dst.Size;
        double wealthBefore = src.Wealth;

        new InteriorPhase().Run(state);

        Assert.True(src.Size < srcBefore, "refugees should leave the famine");
        Assert.True(dst.Size > dstBefore, "and arrive at the fed port");
        Assert.True(src.Wealth < wealthBefore, "wealth travels with people");
    }

    [Fact]
    public void Migration_CreatesADiasporaSegment_WhenCultureDiffers()
    {
        var (state, src, dst) = TwoPortFixture();
        // a second culture at the source (conquest or prior migration)
        var minority = new PopulationSegment(2, 0, src.SpeciesId,
            cultureId: src.CultureId + 1, size: 1.0)
        { Wealth = 50, LastSubsistence = 0.2 };
        state.Segments.Add(minority);
        src.LastSubsistence = 1.0;        // the majority stays
        dst.LastSubsistence = 1.0;
        dst.SoL = 0.9;
        new InteriorPhase().Run(state);

        PopulationSegment? diaspora = null;
        foreach (var s in state.Segments)
            if (s.PortId == 1 && s.CultureId == minority.CultureId) diaspora = s;
        Assert.NotNull(diaspora);    // identity travelled: a new segment at B
        Assert.True(diaspora!.Size > 0);
    }

    [Fact]
    public void Ideology_DriftsWithLivedConditions()
    {
        var (state, starving, prosperous) = TwoPortFixture();
        starving.LastSubsistence = 0.3;
        prosperous.LastSubsistence = 1.0;
        prosperous.SoL = 0.9;

        new InteriorPhase().Run(state);

        Assert.True(starving.Ideology[(int)IdeologyAxis.AuthorityAutonomy] < 0.5,
            "famine turns toward Authority");
        Assert.True(starving.Ideology[(int)IdeologyAxis.SacralMaterial] < 0.5,
            "famine turns toward the Sacral");
        Assert.True(prosperous.Ideology[(int)IdeologyAxis.CommunalIndividual] > 0.5,
            "prosperity turns toward the Individual");
        Assert.True(prosperous.Ideology[(int)IdeologyAxis.OpenInsular] < 0.5,
            "prosperity turns toward the Open");
    }

    [Fact]
    public void PortCap_IsSharedAcrossSegments()
    {
        var (state, src, _) = TwoPortFixture();
        // stuff the source port to its tier cap with a second segment
        double cap = state.Ports[0].Tier * state.Config.Expansion.SegmentCapPerTier;
        var second = new PopulationSegment(2, 0, src.SpeciesId,
            src.CultureId, cap - src.Size);
        state.Segments.Add(second);
        src.LastSubsistence = 1.0;
        src.SoL = 0.9;
        double before = src.Size + second.Size;

        new InteriorPhase().Run(state);

        double after = 0;
        foreach (var s in state.Segments)
            if (s.PortId == 0) after += s.Size;
        Assert.True(after <= before + 1e-9,
            "a full port has no headroom, whoever holds the people");
    }
}
