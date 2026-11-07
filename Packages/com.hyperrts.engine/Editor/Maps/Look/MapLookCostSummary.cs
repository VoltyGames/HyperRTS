using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>What a cost probe measured, saved as cost.json beside summary.json. Times are medians in milliseconds.</summary>
    [Serializable]
    internal sealed class MapLookCostSummary
    {
        public string map;
        public string quality;
        public string weather;

        /// <summary>Frames each time is the median of.</summary>
        public int frames;

        /// <summary>Features the scene has no switch for, so they were not measured.</summary>
        public List<string> skipped = new();

        public List<MapLookCostPoint> points = new();
    }

    /// <summary>A look point at a zoom: its frame time with everything on, and what each feature costs of it.</summary>
    [Serializable]
    internal sealed class MapLookCostPoint
    {
        public string point;
        public float zoom;
        public float onGpuMs;
        public float onCpuMs;
        public List<MapLookCostSaving> savings = new();
    }

    /// <summary>How much frame time turning one feature off saves; negative when the frame got slower.</summary>
    [Serializable]
    internal sealed class MapLookCostSaving
    {
        public string feature;
        public float gpuMs;
        public float cpuMs;
    }
}
