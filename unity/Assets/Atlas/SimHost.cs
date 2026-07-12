using System;
using System.IO;
using StarGen.Core.Atlas;
using StarGen.Core.Epoch;
using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>The only component that touches sim state (unity-atlas-
    /// design.md writers rule). K1: loads an artifact file into a SimState
    /// and republishes it as an AtlasReadModel under the god eye. Run-seed
    /// and stepping join in K4; the atlas otherwise never mutates.</summary>
    public sealed class SimHost : MonoBehaviour
    {
        /// <summary>Resolved against the Unity project folder's parent (the
        /// repo root) when relative — the seed-42/40-epoch golden by default,
        /// which is exactly the eyeball scenario.</summary>
        [SerializeField]
        private string artifactPath = "tests/Core.Tests/Goldens/slice-b-artifact-seed42.txt";

        public SimState State { get; private set; }
        public AtlasReadModel Model { get; private set; }
        public EyeContext Eye => EyeContext.God(State?.WorldYear ?? 0);
        public string LoadError { get; private set; }

        public event Action Loaded;

        public string ArtifactPath
        {
            get => artifactPath;
            set => artifactPath = value;
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
                using var reader = new StreamReader(path);
                State = ArtifactSerializer.Load(reader);
                Model = new AtlasReadModel(State);
                LoadError = null;
                Loaded?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                State = null;
                Model = null;
                LoadError = ex.Message;
                return false;
            }
        }

        public static string ResolvePath(string path) =>
            Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", path));
    }
}
