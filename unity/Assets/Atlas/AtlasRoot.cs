using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>Routing only: SimHost loads → layers show; camera zoom →
    /// screen-constant layers restyle. No state decisions live here.</summary>
    public sealed class AtlasRoot : MonoBehaviour
    {
        [SerializeField] private SimHost simHost;
        [SerializeField] private StarfieldLayer starfield;
        [SerializeField] private DomainFieldLayer domainField;
        [SerializeField] private NatureFieldLayer natureField;
        [SerializeField] private LatticeLayer lattice;
        [SerializeField] private LaneLayer laneLayer;
        [SerializeField] private PortLayer portLayer;
        [SerializeField] private CameraRig cameraRig;

        public SimHost SimHost => simHost;
        public StarfieldLayer Starfield => starfield;
        public DomainFieldLayer DomainField => domainField;
        public NatureFieldLayer NatureField => natureField;
        public LatticeLayer Lattice => lattice;
        public LaneLayer LaneLayer => laneLayer;
        public PortLayer PortLayer => portLayer;
        public CameraRig CameraRig => cameraRig;

        public void Wire(SimHost host, StarfieldLayer stars,
                         DomainFieldLayer domains, NatureFieldLayer nature,
                         LatticeLayer grid, LaneLayer lanes, PortLayer ports,
                         CameraRig rig)
        {
            simHost = host;
            starfield = stars;
            domainField = domains;
            natureField = nature;
            lattice = grid;
            laneLayer = lanes;
            portLayer = ports;
            cameraRig = rig;
        }

        private void OnEnable()
        {
            if (simHost != null) simHost.Loaded += OnLoaded;
            if (cameraRig != null) cameraRig.ZoomChanged += OnZoomChanged;
        }

        private void OnDisable()
        {
            if (simHost != null) simHost.Loaded -= OnLoaded;
            if (cameraRig != null) cameraRig.ZoomChanged -= OnZoomChanged;
        }

        private void OnLoaded()
        {
            var eye = simHost.Eye;
            starfield.Show(simHost.Model);
            domainField.Show(simHost.Model, eye);
            natureField.Show(simHost.Model, eye);
            lattice.Prepare(simHost.Model);
            laneLayer.Show(simHost.Model, eye);
            portLayer.Show(simHost.Model, eye);
            cameraRig.FitTo(AtlasGeometry.DiscBounds(simHost.Model));
            laneLayer.SetExtent(cameraRig.GalaxyExtent);
            OnZoomChanged(cameraRig.Distance);
        }

        private void OnZoomChanged(float distance)
        {
            laneLayer.OnZoom(distance);
            lattice.OnZoom(distance, cameraRig.GalaxyExtent);
        }
    }
}
