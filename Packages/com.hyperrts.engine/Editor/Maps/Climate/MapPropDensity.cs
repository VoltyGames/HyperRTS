using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>Scenery props a surface scatters: rocks on slopes, bales on stubble, logs in woods.</summary>
    [Serializable]
    public sealed class MapPropDensity
    {
        [Tooltip("Palette key.")]
        [MapKey(typeof(MapProp))]
        public string prop;

        [Min(0f)]
        public float perHectare = 1f;

        [Tooltip("Line them up along the field rows (bales left by the baler) instead of scattering them.")]
        public bool alongRows;
    }
}
