using NUnit.Framework;
using StarGen.AtlasView;
using UnityEngine;

namespace StarGen.AtlasView.Tests
{
    /// <summary>K4 — the SimHost writer role over the TimeMachine: a step
    /// or scrub raises TimeChanged (same world, new moment), never Loaded
    /// (new world); keyframes accrue; a scrub returns byte-for-byte to the
    /// base moment's clock.</summary>
    public class SimHostTimeTests
    {
        [Test]
        public void StepAndScrub_RaiseTimeChanged_NeverLoaded()
        {
            var go = new GameObject("SimHostTimeTests");
            try
            {
                var host = go.AddComponent<SimHost>();
                Assert.IsTrue(host.LoadArtifact(), host.LoadError);
                long baseYear = host.State.WorldYear;
                int baseEpoch = host.State.EpochIndex;

                int loads = 0, moments = 0;
                host.Loaded += () => loads++;
                host.TimeChanged += () => moments++;

                host.StepEpochs(1);
                Assert.AreEqual(0, loads, "a step is not a new world");
                Assert.AreEqual(1, moments);
                Assert.AreEqual(2, host.Machine.Keyframes.Count);
                Assert.AreEqual(baseEpoch + 1, host.State.EpochIndex);
                Assert.AreEqual(
                    baseYear + host.State.Config.Sim.YearsPerEpoch,
                    host.State.WorldYear);

                host.ScrubTo(0);
                Assert.AreEqual(0, loads);
                Assert.AreEqual(2, moments);
                Assert.AreEqual(baseYear, host.State.WorldYear);
                Assert.AreEqual(baseEpoch, host.State.EpochIndex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
