using System;
using System.IO;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using StarGen.Core.Galaxy;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The only component that touches sim state (unity-atlas-
    /// design.md writers rule). K1: loads an artifact file into a SimState
    /// and republishes it as an AtlasReadModel under the god eye. K4: the
    /// state lives inside a TimeMachine — stepping captures keyframes,
    /// scrubbing snaps to them, resolution changes fork, and a seed can be
    /// run in-editor (the artifact-load default stays). The atlas is
    /// otherwise a viewer and never writes bases.</summary>
    public sealed class SimHost : MonoBehaviour
    {
        /// <summary>Resolved against the Unity project folder's parent (the
        /// repo root) when relative — the seed-42/40-epoch golden by default,
        /// which is exactly the eyeball scenario.</summary>
        [SerializeField]
        private string artifactPath = "tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt";

        /// <summary>Wall seconds between play-mode steps — the ewatch frame
        /// interval.</summary>
        [SerializeField] private float playStepSeconds = 0.45f;

        public SimState State { get; private set; }
        public AtlasReadModel Model { get; private set; }
        public TimeMachine Machine { get; private set; }
        public EyeContext Eye => EyeContext.God(State?.WorldYear ?? 0);
        public string LoadError { get; private set; }

        /// <summary>A new world arrived (artifact load / run seed): full
        /// rebuild, camera refit, Open Threads greets.</summary>
        public event Action Loaded;

        /// <summary>Same world, new moment (step / scrub / resolution):
        /// layers and open panels re-query; the camera stays put.</summary>
        public event Action TimeChanged;

        public string ArtifactPath
        {
            get => artifactPath;
            set => artifactPath = value;
        }

        // ---- play (the ewatch experience: step-per-interval) ----

        private bool _playing;
        private float _nextStepAt;

        public bool Playing
        {
            get => _playing;
            set
            {
                _playing = value && Machine != null;
                _nextStepAt = 0f;    // first play step lands immediately
            }
        }

        public float PlayStepSeconds
        {
            get => playStepSeconds;
            set => playStepSeconds = Mathf.Max(0.05f, value);
        }

        private void Update()
        {
            if (!_playing || Machine == null) return;
            if (Time.unscaledTime < _nextStepAt) return;
            _nextStepAt = Time.unscaledTime + playStepSeconds;
            StepEpochs(1);
        }

        /// <summary>Play mode loads the default artifact unprompted — the
        /// rail replaced the HUD's load box; K3's chrome takes this over.</summary>
        private void Start()
        {
            if (Model == null) LoadArtifact();
        }

        public bool LoadArtifact()
        {
            try
            {
                string path = ResolvePath(artifactPath);
                // a Windows checkout may hold the artifact as CRLF; the
                // TimeMachine diffs against ToText output, which is LF —
                // normalize at the file boundary or every layer reads
                // changed and genesis strata re-record
                Machine = new TimeMachine(
                    File.ReadAllText(path).Replace("\r\n", "\n"));
                Playing = false;
                Sync();
                LoadError = null;
                Loaded?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                State = null;
                Model = null;
                Machine = null;
                LoadError = ex.Message;
                return false;
            }
        }

        /// <summary>Runs the epoch sim from a seed in-editor (the REPL's
        /// `epoch` pattern) and makes the finished run the timeline's base
        /// artifact — keyframes then record against it like any load.</summary>
        public bool RunSeed(ulong seed, int radiusCells = 21, int epochs = 0)
        {
            try
            {
                var config = new EpochSimConfig { MasterSeed = seed };
                if (epochs > 0) config.Sim.EpochCount = epochs;
                var skeleton = SkeletonBuilder.Build(new GalaxyConfig
                { MasterSeed = seed, GalaxyRadiusCells = radiusCells });
                var state = EpochGenesis.Seed(skeleton, config);
                new EpochEngine().Run(state);
                Machine = new TimeMachine(ArtifactSerializer.ToText(state));
                Playing = false;
                Sync();
                LoadError = null;
                Loaded?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                // deliberate asymmetry with LoadArtifact: a failed run
                // keeps the loaded world viewable (there, the file IS the
                // world, so failure leaves nothing to keep)
                LoadError = ex.Message;
                return false;
            }
        }

        /// <summary>Steps the live timeline (capturing keyframes) and lets
        /// every layer re-query — the writer role, nowhere else.</summary>
        public void StepEpochs(int epochs = 1)
        {
            if (Machine == null) return;
            Machine.Step(epochs);
            Sync();
            TimeChanged?.Invoke();
        }

        /// <summary>Snaps to a keyframe on the active timeline.</summary>
        public void ScrubTo(int keyframeIndex)
        {
            if (Machine == null) return;
            Machine.ScrubTo(keyframeIndex);
            Sync();
            TimeChanged?.Invoke();
        }

        /// <summary>Changes the integration step; mid-run this forks a
        /// branch from the current keyframe (spec §Time).</summary>
        public void SetResolution(int yearsPerEpoch)
        {
            if (Machine == null) return;
            Machine.SetResolution(yearsPerEpoch);
            Sync();
            TimeChanged?.Invoke();
        }

        private void Sync()
        {
            State = Machine.Current;
            Model = new AtlasReadModel(State);
        }

        public static string ResolvePath(string path) =>
            Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path));
    }
}
