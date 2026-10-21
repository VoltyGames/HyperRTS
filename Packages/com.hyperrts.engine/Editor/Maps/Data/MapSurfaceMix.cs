using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A noise-shaped mix of another terrain layer within a surface, such as worn dirt on a base or greener hollows in
    /// dry grass, so big open areas don't read as one texture.
    /// </summary>
    [Serializable]
    public sealed class MapSurfaceMix
    {
        [Tooltip("Key of the terrain layer the mix paints.")]
        [MapKey(typeof(MapClimateLayer))]
        public string layer;

        [Tooltip("Share of the surface the mix covers, 0 to 1.")]
        public float coverage;

        [Tooltip("Metres across the largest blotches; the noise adds smaller ones down to an eighth of that.")]
        public float size;

        public bool Valid => coverage > 0f && coverage <= 1f && size > 0f;
    }
}
