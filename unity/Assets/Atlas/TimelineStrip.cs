using System.Collections.Generic;
using StarGen.Core.Atlas;
using UnityEngine;
using UnityEngine.UIElements;

namespace StarGen.AtlasView
{
    /// <summary>The timeline strip (K4, bottom chrome): era bands
    /// (EraQueries), event-density sparkline (TimelineQueries), keyframe
    /// ticks, world-year scrubber, active marker, and the transport —
    /// play/step at coarse or fine tick, the resolution fork, and the
    /// in-editor run-seed box. All mutation goes through SimHost (the
    /// writer role); this is draw + input only.</summary>
    [RequireComponent(typeof(AtlasChrome))]
    public sealed class TimelineStrip : MonoBehaviour
    {
        [SerializeField] private AtlasRoot root;

        private VisualElement _track;
        private VisualElement _marker;
        private TextField _seed;
        /// <summary>While a scrub-drag is live the strip must not rebuild
        /// (that would destroy the captured track mid-drag) — only the
        /// marker moves; the full rebuild lands on pointer-up.</summary>
        private bool _dragging;

        public void Wire(AtlasRoot atlasRoot) => root = atlasRoot;

        private SimHost Host => root != null ? root.SimHost : null;

        private void OnEnable()
        {
            if (Host != null)
            {
                Host.Loaded += Rebuild;
                Host.TimeChanged += Rebuild;
            }
            Rebuild();
        }

        private void OnDisable()
        {
            if (Host != null)
            {
                Host.Loaded -= Rebuild;
                Host.TimeChanged -= Rebuild;
            }
        }

        private void Rebuild()
        {
            var host = Host;
            if (_dragging && _marker != null && host?.Machine != null)
            {
                long end = AxisEnd(host);
                if (end > 0)
                    _marker.style.left = Length.Percent(
                        100f * host.State.WorldYear / end);
                return;
            }
            var strip = GetComponent<AtlasChrome>().Timeline;
            if (strip == null) return;
            strip.Clear();
            if (host?.Machine == null || host.Model == null) return;

            strip.Add(BuildBar(host));
            strip.Add(BuildTrack(host));
            strip.Add(BuildAxis(host));
        }

        // ---- transport + readouts ----

        private VisualElement BuildBar(SimHost host)
        {
            var bar = new VisualElement();
            bar.AddToClassList("ssg-strip__bar");

            var machine = host.Machine;
            Transport(bar, "|<", () => host.ScrubTo(0));
            Transport(bar, "<", () =>
            {
                if (machine.Position > 0) host.ScrubTo(machine.Position - 1);
            });
            Transport(bar, host.Playing ? "PAUSE" : "PLAY",
                () => { host.Playing = !host.Playing; Rebuild(); },
                accent: true);
            Transport(bar, ">", () => host.StepEpochs(1));
            Transport(bar, ">>", () => host.StepEpochs(5));

            var readout = new Label(DockKit.Inv(
                $"kf {machine.Position}/{machine.Keyframes.Count - 1}"));
            readout.AddToClassList("ssg-strip__readout");
            bar.Add(readout);

            // resolution: coarse generation · 5y · fine — a change mid-run
            // forks a branch from the current keyframe (spec §Time)
            var res = new VisualElement();
            res.AddToClassList("ssg-strip__res");
            int generation = host.State.Config.Sim.GenerationYears;
            int active = machine.Branches[machine.ActiveBranch].Resolution;
            foreach (int years in Ticks(generation))
            {
                int y = years;
                var chip = new Button(() => host.SetResolution(y))
                { text = DockKit.Inv($"{y}y") };
                chip.AddToClassList("ssg-btn");
                if (y == active) chip.AddToClassList("ssg-btn--accent");
                res.Add(chip);
            }
            bar.Add(res);

            if (machine.Branches.Count > 1)
            {
                var branch = machine.Branches[machine.ActiveBranch];
                var chip = new Label(DockKit.Inv(
                    $"FORK b{machine.ActiveBranch} @ kf{branch.ForkedAtKeyframe}"));
                chip.AddToClassList("ssg-strip__branch");
                bar.Add(chip);
            }

            var spacer = new VisualElement();
            spacer.AddToClassList("ssg-spacer");
            bar.Add(spacer);

            _seed = new TextField
            { value = DockKit.Inv($"{host.State.Config.MasterSeed}") };
            _seed.AddToClassList("ssg-strip__seed");
            bar.Add(_seed);
            var run = new Button(() =>
            {
                if (ulong.TryParse(_seed.value.Trim(), out var seed))
                    host.RunSeed(seed);
            }) { text = "RUN SEED" };
            run.AddToClassList("ssg-btn");
            run.AddToClassList("ssg-btn--accent");
            bar.Add(run);

            return bar;
        }

        private static IEnumerable<int> Ticks(int generation)
        {
            yield return generation;
            if (generation != 5) yield return 5;
            if (generation != 1) yield return 1;
        }

        private void Transport(VisualElement bar, string text,
                               System.Action onClick, bool accent = false)
        {
            var btn = new Button(() => onClick()) { text = text };
            btn.AddToClassList("ssg-btn");
            if (accent) btn.AddToClassList("ssg-btn--accent");
            btn.style.marginRight = 4;
            bar.Add(btn);
        }

        // ---- the track ----

        private VisualElement BuildTrack(SimHost host)
        {
            _track = new VisualElement();
            _track.AddToClassList("ssg-strip__track");

            long axisEnd = AxisEnd(host);
            if (axisEnd <= 0) return _track;

            // era bands, quiet gaps included (bands abut in year order)
            var eras = new VisualElement();
            eras.AddToClassList("ssg-strip__eras");
            long covered = 0;
            foreach (var era in EraQueries.Eras(host.Model, host.Eye))
            {
                if (era.StartYear > covered)
                    eras.Add(EraBand(EraKindClass(null),
                        era.StartYear - covered, axisEnd));
                eras.Add(EraBand(EraKindClass(era.Kind),
                    era.EndYear - era.StartYear, axisEnd));
                covered = era.EndYear;
            }
            if (covered < axisEnd)
                eras.Add(EraBand(EraKindClass(null), axisEnd - covered, axisEnd));
            _track.Add(eras);

            // event-density sparkline
            var spark = new VisualElement();
            spark.AddToClassList("ssg-strip__spark");
            var buckets = TimelineQueries.EventDensity(host.Model, host.Eye);
            int max = 1;
            foreach (var b in buckets) if (b.Count > max) max = b.Count;
            foreach (var b in buckets)
            {
                var bin = new VisualElement();
                bin.AddToClassList("ssg-strip__bin");
                bin.style.width = Length.Percent(
                    100f * (b.EndYear - b.StartYear) / axisEnd);
                bin.style.height = Length.Percent(100f * b.Count / max);
                spark.Add(bin);
            }
            _track.Add(spark);

            // keyframe ticks, then the active-year marker on top
            foreach (var frame in host.Machine.Keyframes)
            {
                var tick = new VisualElement();
                tick.AddToClassList("ssg-strip__frame");
                tick.style.left = Length.Percent(100f * frame.WorldYear / axisEnd);
                _track.Add(tick);
            }
            _marker = new VisualElement();
            _marker.AddToClassList("ssg-strip__marker");
            _marker.style.left = Length.Percent(
                100f * host.State.WorldYear / axisEnd);
            _track.Add(_marker);

            // the scrubber: press or drag snaps to the nearest keyframe
            var track = _track;
            track.RegisterCallback<PointerDownEvent>(e =>
            {
                _dragging = true;
                track.CapturePointer(e.pointerId);
                ScrubAt(e.localPosition.x);
            });
            track.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (track.HasPointerCapture(e.pointerId))
                    ScrubAt(e.localPosition.x);
            });
            track.RegisterCallback<PointerUpEvent>(e =>
            {
                if (track.HasPointerCapture(e.pointerId))
                    track.ReleasePointer(e.pointerId);
                _dragging = false;
                Rebuild();
            });

            return _track;
        }

        private VisualElement BuildAxis(SimHost host)
        {
            var axis = new VisualElement();
            axis.AddToClassList("ssg-strip__axis");
            var start = new Label("y0");
            start.AddToClassList("ssg-strip__year");
            axis.Add(start);
            var now = new Label(DockKit.Inv(
                $"y{host.State.WorldYear} · epoch {host.State.EpochIndex}"));
            now.AddToClassList("ssg-strip__year");
            axis.Add(now);
            var end = new Label(DockKit.Inv($"y{AxisEnd(host)}"));
            end.AddToClassList("ssg-strip__year");
            axis.Add(end);
            return axis;
        }

        /// <summary>The axis spans the whole recorded history: the log's
        /// year 0 through the farthest of the live year and the timeline's
        /// tip (a scrub-back must not shrink the strip).</summary>
        private static long AxisEnd(SimHost host)
        {
            long end = host.State.WorldYear;
            var frames = host.Machine.Keyframes;
            if (frames.Count > 0 && frames[frames.Count - 1].WorldYear > end)
                end = frames[frames.Count - 1].WorldYear;
            return end;
        }

        private static VisualElement EraBand(string kindClass, long years,
                                             long axisEnd)
        {
            var band = new VisualElement();
            band.AddToClassList("ssg-strip__era");
            band.AddToClassList(kindClass);
            band.style.width = Length.Percent(100f * years / axisEnd);
            return band;
        }

        private static string EraKindClass(Core.Epoch.EraKind? kind) =>
            kind switch
            {
                Core.Epoch.EraKind.Expansion => "ssg-strip__era--expansion",
                Core.Epoch.EraKind.Treaty => "ssg-strip__era--treaty",
                Core.Epoch.EraKind.Upheaval => "ssg-strip__era--upheaval",
                Core.Epoch.EraKind.War => "ssg-strip__era--war",
                _ => "ssg-strip__era--quiet",
            };

        /// <summary>Snap the pointer's track position to the nearest
        /// keyframe year and scrub there (no-op when already on it).</summary>
        private void ScrubAt(float x)
        {
            var host = Host;
            if (host?.Machine == null) return;
            float width = _track.resolvedStyle.width;
            long axisEnd = AxisEnd(host);
            if (width <= 0 || axisEnd <= 0) return;
            double year = (double)Mathf.Clamp01(x / width) * axisEnd;

            var frames = host.Machine.Keyframes;
            int nearest = 0;
            double best = double.MaxValue;
            for (int i = 0; i < frames.Count; i++)
            {
                double d = System.Math.Abs(frames[i].WorldYear - year);
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest != host.Machine.Position) host.ScrubTo(nearest);
        }
    }
}
