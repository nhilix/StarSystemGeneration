using UnityEngine;

namespace StarGen.AtlasView
{
    /// <summary>Routing only: SimHost loads → layers show; camera zoom →
    /// screen-constant layers restyle. No state decisions live here —
    /// which lenses are visible is the LensRail's call.</summary>
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
        [SerializeField] private FleetLayer fleetLayer;
        [SerializeField] private PoiLayer poiLayer;
        [SerializeField] private WorksLayer worksLayer;
        [SerializeField] private PlagueLayer plagueLayer;
        [SerializeField] private WarLayer warLayer;
        [SerializeField] private NewsLayer newsLayer;
        [SerializeField] private PriceFieldLayer priceField;

        public SimHost SimHost => simHost;
        public StarfieldLayer Starfield => starfield;
        public DomainFieldLayer DomainField => domainField;
        public NatureFieldLayer NatureField => natureField;
        public LatticeLayer Lattice => lattice;
        public LaneLayer LaneLayer => laneLayer;
        public PortLayer PortLayer => portLayer;
        public CameraRig CameraRig => cameraRig;
        public FleetLayer FleetLayer => fleetLayer;
        public PoiLayer PoiLayer => poiLayer;
        public WorksLayer WorksLayer => worksLayer;
        public PlagueLayer PlagueLayer => plagueLayer;
        public WarLayer WarLayer => warLayer;
        public NewsLayer NewsLayer => newsLayer;
        public PriceFieldLayer PriceField => priceField;

        public void Wire(SimHost host, StarfieldLayer stars,
                         DomainFieldLayer domains, NatureFieldLayer nature,
                         LatticeLayer grid, LaneLayer lanes, PortLayer ports,
                         CameraRig rig, FleetLayer fleets, PoiLayer pois,
                         WorksLayer works, PlagueLayer plague, WarLayer war,
                         NewsLayer news, PriceFieldLayer price)
        {
            simHost = host;
            starfield = stars;
            domainField = domains;
            natureField = nature;
            lattice = grid;
            laneLayer = lanes;
            portLayer = ports;
            cameraRig = rig;
            fleetLayer = fleets;
            poiLayer = pois;
            worksLayer = works;
            plagueLayer = plague;
            warLayer = war;
            newsLayer = news;
            priceField = price;
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
            var model = simHost.Model;
            starfield.Show(model);
            domainField.Show(model, eye);
            natureField.Show(model, eye);
            lattice.Prepare(model);
            laneLayer.Show(model, eye);
            portLayer.Show(model, eye);
            fleetLayer.Show(model, eye);
            poiLayer.Show(model, eye);
            worksLayer.Show(model, eye);
            plagueLayer.Show(model, eye);
            warLayer.Show(model, eye);
            newsLayer.Show(model, eye);
            priceField.Show(model, eye);
            cameraRig.FitTo(AtlasGeometry.DiscBounds(model));
            laneLayer.SetExtent(cameraRig.GalaxyExtent);
            OnZoomChanged(cameraRig.Distance);
        }

        private void OnZoomChanged(float distance)
        {
            laneLayer.ViewportPx = Mathf.Max(1, cameraRig.Cam.pixelHeight);
            laneLayer.OnZoom(distance);
            lattice.OnZoom(distance, cameraRig.GalaxyExtent);
            float extent = cameraRig.GalaxyExtent;
            fleetLayer.OnZoom(distance, extent);
            poiLayer.OnZoom(distance, extent);
            worksLayer.OnZoom(distance, extent);
            plagueLayer.OnZoom(distance, extent);
            warLayer.OnZoom(distance, extent);
        }
    }
}
