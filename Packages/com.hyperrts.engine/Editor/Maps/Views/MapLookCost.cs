using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Measures one look view's median GPU and main-thread frame time with everything on, then with each feature off
    /// in turn, a few frames at a time as the run steps.
    /// </summary>
    internal sealed class MapLookCost : IDisposable
    {
        public const int Frames = 120;

        // Frames after a switch for GPU buffers to rebuild and the frame timings to reflect it.
        private const int SettleFrames = 60;

        private readonly MapLookSwitches _switches;
        private readonly List<string> _features;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
        private readonly List<float> _gpu = new();
        private readonly List<float> _cpu = new();
        private MapLookCostPoint _point;
        private int _variant;
        private int _lastFrame = -1;
        private int _settleUntil;
        private int _sampled;

        public MapLookCost(Camera camera, string map, string quality, string weather)
        {
            if (!FrameTimingManager.IsFeatureEnabled())
            {
                throw new InvalidOperationException("HyperRTS: frame timing stats are off, so the cost probe has no GPU times.");
            }

            _switches = new MapLookSwitches(camera);
            _features = _switches.Names.ToList();
            Summary = new MapLookCostSummary
            {
                map = map, quality = quality, weather = weather, frames = Frames, skipped = _switches.Missing,
            };
        }

        public MapLookCostSummary Summary { get; }

        public void Dispose() => _switches.Dispose();

        /// <summary>Advances the measurement of a view by a frame; true when its point is complete.</summary>
        public bool Measure(string point, float zoom)
        {
            if (_point == null)
            {
                _point = new MapLookCostPoint { point = point, zoom = zoom };
                _settleUntil = Time.frameCount;
            }

            if (Time.frameCount == _lastFrame)
            {
                return false;
            }

            _lastFrame = Time.frameCount;
            if (Time.frameCount < _settleUntil)
            {
                return false;
            }

            Sample();
            if (_sampled < Frames)
            {
                return false;
            }

            return Finish();
        }

        private void Sample()
        {
            FrameTimingManager.CaptureFrameTimings();
            _sampled++;
            if (FrameTimingManager.GetLatestTimings(1, _timing) == 1 && _timing[0].gpuFrameTime > 0)
            {
                _gpu.Add((float)_timing[0].gpuFrameTime);
                _cpu.Add((float)_timing[0].cpuMainThreadFrameTime);
            }
        }

        // Records the variant just sampled and switches to the next; true after the last one.
        private bool Finish()
        {
            if (_gpu.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: the GPU reported no frame times at '{_point.point}'.");
            }

            var (gpu, cpu) = (Median(_gpu), Median(_cpu));
            _gpu.Clear();
            _cpu.Clear();
            _sampled = 0;
            if (_variant == 0)
            {
                (_point.onGpuMs, _point.onCpuMs) = (gpu, cpu);
            }
            else
            {
                var feature = _features[_variant - 1];
                _switches.Set(feature, true);
                _point.savings.Add(new MapLookCostSaving
                {
                    feature = feature, gpuMs = _point.onGpuMs - gpu, cpuMs = _point.onCpuMs - cpu,
                });
            }

            if (_variant == _features.Count)
            {
                Summary.points.Add(_point);
                (_point, _variant) = (null, 0);
                return true;
            }

            _switches.Set(_features[_variant], false);
            _variant++;
            _settleUntil = Time.frameCount + SettleFrames;
            return false;
        }

        private static float Median(List<float> times)
        {
            var sorted = times.OrderBy(time => time).ToList();
            return sorted[sorted.Count / 2];
        }
    }
}
