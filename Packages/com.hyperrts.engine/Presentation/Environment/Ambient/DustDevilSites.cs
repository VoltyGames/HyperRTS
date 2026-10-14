using System;
using HyperRTS.Presentation.Environment.RowCrops;
using HyperRTS.Simulation.Effects;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// Where a dust devil may form: random ground near a point whose material is on the allowed list and that no row
    /// crop covers (crop fields read as soil).
    /// </summary>
    public static class DustDevilSites
    {
        public const int Attempts = 8;

        /// <summary>
        /// A random point within <paramref name="reach"/> of <paramref name="focus"/> on an allowed material outside
        /// <paramref name="crops"/>; false when <see cref="Attempts"/> samples all miss.
        /// </summary>
        public static bool TryPick(in GroundMaterials ground, RowCropCover crops, float2 focus, float reach,
            SurfaceMaterial[] allowed, ref Random random, out float2 site)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                site = focus + random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * reach;
                var allowedGround = Array.IndexOf(allowed, ground.At(site)) >= 0;
                if (allowedGround && !crops.Covers(site))
                {
                    return true;
                }
            }

            site = default;
            return false;
        }
    }
}
