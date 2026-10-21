using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// Noise patches of another terrain layer within a surface, such as worn dirt on a base or greener hollows in
    /// dry grass, so big open areas don't read as one texture.
    /// </summary>
    [Serializable]
    public sealed class MapSurfacePatch
    {
        [Tooltip("Key of the terrain layer the patches paint.")]
        [MapKey(typeof(MapClimateLayer))]
        public string layer;

        [Tooltip("Share of the surface the patches cover, 0 to 1.")]
        public float coverage;

        [Tooltip("Metres across the largest patches; the noise adds patches down to an eighth of that.")]
        public float size;

        public bool Valid => coverage > 0f && coverage <= 1f && size > 0f;
    }
}
