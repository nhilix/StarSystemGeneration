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
        [SerializeField] private DomainInteriorLayer domainInterior;
        [SerializeField] private OutpostLayer outpostLayer;
        [SerializeField] private NatureFieldLayer natureField;
        [SerializeField] private LatticeLayer lattice;
        [SerializeField] private LaneLayer laneLayer;
        [SerializeField] private PortLayer portLayer;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private FleetLayer fleetLayer;
        [SerializeField] private PoiLayer poiLayer;
        [SerializeField] private WorksLayer worksLayer;
        [SerializeField] private FlowTrailLayer flowTrailLayer;
        [SerializeField] private PlagueLayer plagueLayer;
        [SerializeField] private WarLayer warLayer;
        [SerializeField] private NewsLayer newsLayer;
        [SerializeField] private PriceFieldLayer priceField;

        public SimHost SimHost => simHost;
        public StarfieldLayer Starfield => starfield;
        public DomainFieldLayer DomainField => domainField;
        public DomainInteriorLayer DomainInterior => domainInterior;
        public OutpostLayer OutpostLayer => outpostLayer;
        public NatureFieldLayer NatureField => natureField;
        public LatticeLayer Lattice => lattice;
        public LaneLayer LaneLayer => laneLayer;
        public PortLayer PortLayer => portLayer;
        public CameraRig CameraRig => cameraRig;
        public FleetLayer FleetLayer => fleetLayer;
        public PoiLayer PoiLayer => poiLayer;
        public WorksLayer WorksLayer => worksLayer;
        public FlowTrailLayer FlowTrailLayer => flowTrailLayer;
        public PlagueLayer PlagueLayer => plagueLayer;
        public WarLayer WarLayer => warLayer;
        public NewsLayer NewsLayer => newsLayer;
        public PriceFieldLayer PriceField => priceField;

        public void Wire(SimHost host, StarfieldLayer stars,
                         DomainFieldLayer domains, DomainInteriorLayer interior,
                         OutpostLayer outposts, NatureFieldLayer nature,
                         LatticeLayer grid, LaneLayer lanes, PortLayer ports,
                         CameraRig rig, FleetLayer fleets, PoiLayer pois,
                         WorksLayer works, PlagueLayer plague, WarLayer war,
                         NewsLayer news, PriceFieldLayer price,
                         FlowTrailLayer flowTrails)
        {
            simHost = host;
            starfield = stars;
            domainField = domains;
            domainInterior = interior;
            outpostLayer = outposts;
            natureField = nature;
            lattice = grid;
            laneLayer = lanes;
            portLayer = ports;
            cameraRig = rig;
            fleetLayer = fleets;
            poiLayer = pois;
            worksLayer = works;
            flowTrailLayer = flowTrails;
            plagueLayer = plague;
            warLayer = war;
            newsLayer = news;
            priceField = price;
        }

        private void OnEnable()
        {
            if (simHost != null)
            {
                simHost.Loaded += OnLoaded;
                simHost.TimeChanged += OnTimeChanged;
            }
            if (cameraRig != null) cameraRig.ZoomChanged += OnZoomChanged;
        }

        private void OnDisable()
        {
            if (simHost != null)
            {
                simHost.Loaded -= OnLoaded;
                simHost.TimeChanged -= OnTimeChanged;
            }
            if (cameraRig != null) cameraRig.ZoomChanged -= OnZoomChanged;
        }

        private void OnLoaded()
        {
            ShowAll();
            cameraRig.FitTo(AtlasGeometry.DiscBounds(simHost.Model));
            laneLayer.SetExtent(cameraRig.GalaxyExtent);
            if (flowTrailLayer != null)
                flowTrailLayer.SetExtent(cameraRig.GalaxyExtent);
            OnZoomChanged(cameraRig.Distance);
        }

        /// <summary>Same world, new moment (step/scrub): every layer
        /// re-queries; the camera stays where the user left it.</summary>
        private void OnTimeChanged()
        {
            ShowAll();
            OnZoomChanged(cameraRig.Distance);
        }

        private void ShowAll()
        {
            var eye = simHost.Eye;
            var model = simHost.Model;
            starfield.Show(model);
            domainField.Show(model, eye);
            domainInterior.Show(model, eye);
            outpostLayer.Show(model, eye);
            natureField.Show(model, eye);
            lattice.Prepare(model);
            laneLayer.Show(model, eye);
            portLayer.Show(model, eye);
            fleetLayer.Show(model, eye);
            poiLayer.Show(model, eye);
            worksLayer.Show(model, eye);
            // AC2.F2: the trails read the TimeMachine's per-keyframe flow
            // capture (in-memory beside the keyframe, never on the state) —
            // null-guarded so an older serialized scene stays alive until
            // the setup regenerates it
            if (flowTrailLayer != null)
                flowTrailLayer.Show(model, simHost.Machine != null
                    ? simHost.Machine.CurrentFlows
                    : System.Array.Empty<StarGen.Core.Atlas.RecentFlow>());
            plagueLayer.Show(model, eye);
            warLayer.Show(model, eye);
            newsLayer.Show(model, eye);
            priceField.Show(model, eye);
        }

        private void OnZoomChanged(float distance)
        {
            laneLayer.ViewportPx = Mathf.Max(1, cameraRig.Cam.pixelHeight);
            laneLayer.OnZoom(distance);
            if (flowTrailLayer != null)
            {
                flowTrailLayer.ViewportPx = Mathf.Max(1, cameraRig.Cam.pixelHeight);
                flowTrailLayer.OnZoom(distance);
            }
            lattice.OnZoom(distance, cameraRig.GalaxyExtent);
            float extent = cameraRig.GalaxyExtent;
            fleetLayer.OnZoom(distance, extent);
            poiLayer.OnZoom(distance, extent);
            worksLayer.OnZoom(distance, extent);
            plagueLayer.OnZoom(distance, extent);
            warLayer.OnZoom(distance, extent);
            // the K5 hex→orbit crossfade: every remaining map layer
            // dissolves as the stage fades up (starfield stays — space
            // is still space under the orbit view)
            portLayer.OnZoom(distance, extent);
            outpostLayer.OnZoom(distance, extent);
            domainInterior.OnZoom(distance, extent);
            newsLayer.OnZoom(distance, extent);
            domainField.OnZoom(distance, extent);
            natureField.OnZoom(distance, extent);
            priceField.OnZoom(distance, extent);
        }
    }
}
