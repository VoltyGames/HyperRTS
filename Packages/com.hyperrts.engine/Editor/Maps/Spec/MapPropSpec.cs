using System;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>One palette prefab: a neutral structure (derrick, outpost) or a piece of scenery.</summary>
    [Serializable]
    public sealed class MapPropSpec
    {
        public MapPoint pos;
        public float yaw;
        [MapKey(typeof(MapProp))]
        public string kind;
        public bool shared;
    }
}
