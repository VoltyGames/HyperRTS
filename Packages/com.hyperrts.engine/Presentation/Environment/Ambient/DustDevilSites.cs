using System;
using HyperRTS.Simulation.Cues;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Where a dust devil may form: random ground near a point whose material is on the allowed list.
    /// </summary>
    public static class DustDevilSites
    {
        public const int Attempts = 8;

        /// <summary>
        /// A random point within <paramref name="reach"/> of <paramref name="focus"/> on an allowed material; false when <see cref="Attempts"/> samples all miss.
        /// </summary>
        public static bool TryPick(in GroundMaterials ground, float2 focus, float reach,
            SurfaceMaterial[] allowed, ref Random random, out float2 site)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                site = focus + random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * reach;
                if (Array.IndexOf(allowed, ground.At(site)) >= 0)
                {
                    return true;
                }
            }

            site = default;
            return false;
        }
    }
}
