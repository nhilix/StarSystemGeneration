using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarGen.AtlasView
{
    /// <summary>One camera on one scene (unity-atlas-design.md pillar 1):
    /// scroll zooms toward the cursor, right/middle drag and WASD pan.
    /// Publishes the LOD band; the band table itself lives in LodBands.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        private float _minOrtho = 2f;
        private float _maxOrtho = 100f;
        private float _galaxyExtent = 100f;
        private Vector3 _dragOrigin;
        private bool _dragging;

        public LodBand Band { get; private set; } = LodBand.Galaxy;
        public event Action<LodBand> BandChanged;

        public Camera Cam => cam;

        public void Configure(Camera camera) => cam = camera;

        /// <summary>Frame the whole disc and calibrate the band thresholds
        /// to its extent.</summary>
        public void FitTo(Bounds bounds)
        {
            _galaxyExtent = Mathf.Max(bounds.extents.y,
                                      bounds.extents.x / Mathf.Max(0.1f, cam.aspect));
            _maxOrtho = _galaxyExtent * 1.08f;
            _minOrtho = 3f;
            cam.orthographic = true;
            cam.orthographicSize = _maxOrtho;
            cam.transform.position =
                new Vector3(bounds.center.x, bounds.center.y, -10f);
            PublishBand();
        }

        private void Update()
        {
            if (cam == null) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) ZoomToward(mouse.position.ReadValue(), scroll);

                bool holdPan = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                if (holdPan && !_dragging)
                {
                    _dragging = true;
                    _dragOrigin = WorldAt(mouse.position.ReadValue());
                }
                else if (holdPan)
                {
                    var now = WorldAt(mouse.position.ReadValue());
                    cam.transform.position += _dragOrigin - now;
                }
                else
                {
                    _dragging = false;
                }
            }

            if (keyboard != null)
            {
                var move = Vector3.zero;
                if (keyboard.wKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed) move.y -= 1;
                if (keyboard.aKey.isPressed) move.x -= 1;
                if (keyboard.dKey.isPressed) move.x += 1;
                if (move != Vector3.zero)
                    cam.transform.position +=
                        move.normalized * (cam.orthographicSize * 1.2f * Time.deltaTime);
            }
        }

        private void ZoomToward(Vector2 screenPos, float scroll)
        {
            var before = WorldAt(screenPos);
            float factor = Mathf.Pow(1.12f, -Mathf.Sign(scroll));
            cam.orthographicSize =
                Mathf.Clamp(cam.orthographicSize * factor, _minOrtho, _maxOrtho);
            var after = WorldAt(screenPos);
            cam.transform.position += before - after;
            PublishBand();
        }

        private Vector3 WorldAt(Vector2 screenPos)
        {
            var world = cam.ScreenToWorldPoint(
                new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            world.z = 0f;
            return world;
        }

        private void PublishBand()
        {
            var band = LodBands.BandFor(cam.orthographicSize, _galaxyExtent);
            if (band == Band) return;
            Band = band;
            BandChanged?.Invoke(band);
        }
    }
}
