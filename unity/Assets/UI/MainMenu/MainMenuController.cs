using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.MenuView
{
    /// <summary>Entry-scene controller. C# owns state — deterministic
    /// starfield spawn (same seed, same sky), cursor blink, stub actions;
    /// USS owns all appearance (hover inversion, transitions).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const int StarCount = 130;
        private const long BlinkMs = 530;
        private const string CursorOffClass = "ssg-cursor--off";

        private VisualElement _starfield;
        private TextField _seed;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _starfield = root.Q<VisualElement>("starfield");
            _seed = root.Q<TextField>("seed");

            _seed.RegisterValueChangedCallback(_ => SpawnStars());
            Wire(root, "row-new", () => Stub($"generate seed '{_seed.value}'"));
            Wire(root, "row-continue", () => Stub("continue"));
            Wire(root, "row-load", () => Stub("load galaxy"));
            Wire(root, "row-settings", () => Stub("settings"));
            Wire(root, "row-quit", Quit);

            var cursors = new[] { root.Q<Label>("cursor-status"), root.Q<Label>("cursor-prompt") };
            root.schedule.Execute(() =>
            {
                foreach (var c in cursors) c?.ToggleInClassList(CursorOffClass);
            }).Every(BlinkMs);

            SpawnStars();
        }

        private void Wire(VisualElement root, string rowName, Action action)
        {
            root.Q<VisualElement>(rowName)?.RegisterCallback<ClickEvent>(evt =>
            {
                // clicking the seed field must not fire the row action
                if (evt.target is VisualElement t && IsInTextField(t)) return;
                action();
            });
        }

        private static bool IsInTextField(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
                if (v is TextField) return true;
            return false;
        }

        private void SpawnStars()
        {
            if (_starfield == null) return;
            _starfield.Clear();
            var rng = new Mulberry32(Fnv1a((_seed?.value ?? "VOID").Trim().ToUpperInvariant()));
            for (int i = 0; i < StarCount; i++)
            {
                int size = rng.Next01() < 0.86f ? (rng.Next01() < 0.5f ? 1 : 2) : 3;
                var star = new VisualElement { pickingMode = PickingMode.Ignore };
                star.style.position = Position.Absolute;
                star.style.left = Length.Percent(rng.Next01() * 100f);
                star.style.top = Length.Percent(rng.Next01() * 100f);
                star.style.width = size;
                star.style.height = size;
                bool iceTint = rng.Next01() < 0.15f;
                float alpha = 0.25f + rng.Next01() * 0.65f;
                star.style.backgroundColor = iceTint
                    ? new Color(134f / 255f, 215f / 255f, 1f, alpha)
                    : new Color(216f / 255f, 230f / 255f, 250f / 255f, alpha);
                _starfield.Add(star);
            }
        }

        private static void Stub(string action) =>
            Debug.Log($"[MainMenu] {action} — stub until the atlas flow is wired");

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static uint Fnv1a(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }

        /// <summary>Deterministic PRNG, ported from the design mock's
        /// mulberry32 so the committed mock and the scene agree.</summary>
        private struct Mulberry32
        {
            private uint _state;
            public Mulberry32(uint seed) => _state = seed;

            public float Next01()
            {
                _state += 0x6D2B79F5u;
                uint t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296f;
            }
        }
    }
}
