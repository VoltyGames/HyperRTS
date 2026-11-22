using System;
using System.Collections.Generic;

namespace HyperRTS.Input.Cameras
{
    /// <summary>The frame times measured at one point and height, in milliseconds.</summary>
    [Serializable]
    public sealed class CameraCostResult
    {
        public string point;
        public float height;
        public int samples;
        public float gpuMedianMs;
        public float gpuP95Ms;
        public float cpuMedianMs;
        public float cpuP95Ms;

        public static CameraCostResult From(string point, float height, List<float> gpu, List<float> cpu)
        {
            return new CameraCostResult
            {
                point = point,
                height = height,
                samples = gpu.Count,
                gpuMedianMs = Percentile(gpu, 0.5f),
                gpuP95Ms = Percentile(gpu, 0.95f),
                cpuMedianMs = Percentile(cpu, 0.5f),
                cpuP95Ms = Percentile(cpu, 0.95f),
            };
        }

        private static float Percentile(List<float> times, float fraction)
        {
            var sorted = new List<float>(times);
            sorted.Sort();
            return sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * fraction))];
        }
    }
}
