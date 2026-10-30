using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A climate tree and the stripped or snapped species that stand in for it after shelling.</summary>
    [Serializable]
    public sealed class MapWarTreeSwap
    {
        [MapKey(typeof(MapClimateTree))]
        public string tree;

        [MapKey(typeof(MapClimateTree))]
        public List<string> shredded = new();
    }
}
