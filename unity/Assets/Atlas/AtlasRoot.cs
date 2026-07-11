using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>Routing only (the PoC controller lesson): SimHost loads →
    /// surfaces show; camera band changes → lens layers restyle. No state
    /// decisions live here.</summary>
    public sealed class AtlasRoot : MonoBehaviour
    {
        [SerializeField] private SimHost simHost;
        [SerializeField] private MapSurface mapSurface;
        [SerializeField] private LaneLayer laneLayer;
        [SerializeField] private PortLayer portLayer;
        [SerializeField] private CameraRig cameraRig;

        public SimHost SimHost => simHost;
        public MapSurface MapSurface => mapSurface;
        public LaneLayer LaneLayer => laneLayer;
        public PortLayer PortLayer => portLayer;
        public CameraRig CameraRig => cameraRig;

        public void Wire(SimHost host, MapSurface surface, LaneLayer lanes,
                         PortLayer ports, CameraRig rig)
        {
            simHost = host;
            mapSurface = surface;
            laneLayer = lanes;
            portLayer = ports;
            cameraRig = rig;
        }

        private void OnEnable()
        {
            if (simHost != null) simHost.Loaded += OnLoaded;
            if (cameraRig != null) cameraRig.BandChanged += OnBandChanged;
        }

        private void OnDisable()
        {
            if (simHost != null) simHost.Loaded -= OnLoaded;
            if (cameraRig != null) cameraRig.BandChanged -= OnBandChanged;
        }

        private void OnLoaded()
        {
            var eye = simHost.Eye;
            mapSurface.Show(simHost.Model, eye);
            laneLayer.Show(simHost.Model, eye);
            portLayer.Show(simHost.Model, eye);
            cameraRig.FitTo(mapSurface.MapBounds);
            OnBandChanged(cameraRig.Band);
        }

        private void OnBandChanged(LodBand band)
        {
            laneLayer.SetBand(band);
            portLayer.SetBand(band);
        }
    }
}
