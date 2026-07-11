using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarGen.AtlasView
{
    /// <summary>The 2.5D camera (spec "The camera"): a perspective camera
    /// described by focus-point-on-plane + distance + pitch, smoothly
    /// damped toward its targets every frame. Scroll dollies toward the
    /// cursor's plane intersection; right-drag pans on the plane;
    /// middle-drag tilts; the pure top-down view is the 90° pitch limit.
    /// Publishes band changes (what resolves) and continuous zoom (how
    /// things scale).</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        private Vector3 _focus, _targetFocus;
        private float _distance = 200f, _targetDistance = 200f;
        private float _pitch = 55f, _targetPitch = 55f;
        private float _minDistance = 3f, _maxDistance = 600f;
        private float _galaxyExtent = 100f;
        private Vector3 _panAnchor;
        private bool _panning;
        private const float FovDegrees = 50f;
        private const float DampHalfLife = 0.09f;

        public LodBand Band { get; private set; } = LodBand.Galaxy;
        public float Distance => _distance;
        public float GalaxyExtent => _galaxyExtent;
        public float Pitch => _pitch;
        public Camera Cam => cam;

        public event Action<LodBand> BandChanged;
        /// <summary>Fires when the damped distance moved this frame —
        /// layers with screen-constant styling listen here.</summary>
        public event Action<float> ZoomChanged;

        public void Configure(Camera camera)
        {
            cam = camera;
            cam.orthographic = false;
            cam.fieldOfView = FovDegrees;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 3000f;
        }

        /// <summary>Frame the whole disc top-down-ish and calibrate the
        /// thresholds to its extent.</summary>
        public void FitTo(Bounds bounds)
        {
            _galaxyExtent = Mathf.Max(bounds.extents.x, bounds.extents.y);
            float fit = _galaxyExtent /
                        Mathf.Tan(FovDegrees * 0.5f * Mathf.Deg2Rad) * 1.05f;
            _maxDistance = fit * 1.3f;
            _minDistance = 2.5f;
            SetView(new Vector3(bounds.center.x, bounds.center.y, 0f), fit, 65f);
        }

        /// <summary>Jump-cut to a view (tooling/acceptance shots): targets
        /// and current state snap together, transform applied now.</summary>
        public void SetView(Vector3 focus, float distance, float pitch)
        {
            _focus = _targetFocus = focus;
            _distance = _targetDistance =
                Mathf.Clamp(distance, _minDistance, _maxDistance);
            _pitch = _targetPitch = Mathf.Clamp(pitch, 25f, 90f);
            Apply();
            PublishBand();
            ZoomChanged?.Invoke(_distance);
        }

        private void Update()
        {
            if (cam == null) return;
            ReadInput();

            float k = 1f - Mathf.Exp(-Time.deltaTime / DampHalfLife);
            bool moved = false;
            if (Mathf.Abs(_distance - _targetDistance) > 0.0005f * _distance)
            {
                _distance = Mathf.Lerp(_distance, _targetDistance, k);
                moved = true;
            }
            _focus = Vector3.Lerp(_focus, _targetFocus, k);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, k);
            Apply();
            if (moved)
            {
                ZoomChanged?.Invoke(_distance);
                PublishBand();
            }
        }

        private void ReadInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    var toward = PlanePoint(mouse.position.ReadValue());
                    float factor = Mathf.Pow(1.25f, -Mathf.Sign(scroll));
                    float next = Mathf.Clamp(_targetDistance * factor,
                                             _minDistance, _maxDistance);
                    // Dolly toward the cursor: the focus slides so the
                    // point under the cursor stays put as depth shrinks.
                    float pull = 1f - next / _targetDistance;
                    _targetFocus += (toward - _targetFocus) * pull;
                    _targetDistance = next;
                }

                if (mouse.rightButton.isPressed)
                {
                    var now = PlanePoint(mouse.position.ReadValue());
                    if (_panning)
                        // The grabbed world point stays under the cursor:
                        // anchor is fixed at grab time, focus chases the gap.
                        _targetFocus += _panAnchor - now;
                    else
                    {
                        _panAnchor = now;
                        _panning = true;
                    }
                }
                else
                {
                    _panning = false;
                }

                if (mouse.middleButton.isPressed)
                {
                    float dy = mouse.delta.ReadValue().y;
                    _targetPitch = Mathf.Clamp(_targetPitch + dy * 0.2f, 25f, 90f);
                }
            }

            if (keyboard != null)
            {
                var move = Vector2.zero;
                if (keyboard.wKey.isPressed) move.y += 1;
                if (keyboard.sKey.isPressed) move.y -= 1;
                if (keyboard.aKey.isPressed) move.x -= 1;
                if (keyboard.dKey.isPressed) move.x += 1;
                if (move != Vector2.zero)
                {
                    var step = move.normalized * (_targetDistance * 0.9f
                                                  * Time.deltaTime);
                    _targetFocus += new Vector3(step.x, step.y, 0f);
                }
            }
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch - 90f, 0f, 0f);
            cam.transform.rotation = rotation;
            cam.transform.position = _focus - rotation * Vector3.forward * _distance;
        }

        /// <summary>The cursor ray's intersection with the galactic plane.</summary>
        private Vector3 PlanePoint(Vector2 screenPos)
        {
            var ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            float denom = ray.direction.z;
            if (Mathf.Abs(denom) < 1e-5f) return _focus;
            float t = -ray.origin.z / denom;
            if (t < 0f) return _focus;
            var p = ray.origin + ray.direction * t;
            p.z = 0f;
            return p;
        }

        private void PublishBand()
        {
            var band = LodBands.BandFor(_distance, _galaxyExtent);
            if (band == Band) return;
            Band = band;
            BandChanged?.Invoke(band);
        }
    }
}
