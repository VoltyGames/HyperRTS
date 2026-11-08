using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>How the ground is shaped before features carve or raise it. Heights in metres.</summary>
    [Serializable]
    public sealed class MapTerrainSpec
    {
        /// <summary>Height of open ground; rivers and ravines cut below it, hills rise above it.</summary>
        public float height = 20f;

        /// <summary>Amplitude of the rolling noise added to open ground.</summary>
        public float relief = 3f;

        /// <summary>Wavelength of the rolling noise.</summary>
        public float reliefScale = 120f;

        /// <summary>Water surface; ground below it is water. Must be below <see cref="height"/>.</summary>
        public float waterLevel = 12f;

        /// <summary>Top of the terrain's height range; every height must stay below it.</summary>
        public float maxHeight = 60f;

        /// <summary>Heightmap samples per side (2^n + 1).</summary>
        public int resolution = 513;

        /// <summary>
        /// Width of the backdrop: unplayable land beyond the playable edge on every side (metres), so the camera never
        /// looks off the world. Rivers and roads that reach the edge run on into it; areas and rows may be drawn there.
        /// </summary>
        public float backdropWidth;
    }
}
