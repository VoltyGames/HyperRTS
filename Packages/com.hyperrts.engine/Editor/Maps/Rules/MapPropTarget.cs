using System;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Rules
{
    /// <summary>The fewest props per hectare a surface should carry before the report calls it bare.</summary>
    [Serializable]
    public sealed class MapPropTarget
    {
        [Tooltip("Key of the surface.")]
        [MapKey(typeof(MapClimateSurface))]
        public string surface;

        [Tooltip("Fewest props per hectare.")]
        [Min(0f)]
        public float perHectare;
    }
}
