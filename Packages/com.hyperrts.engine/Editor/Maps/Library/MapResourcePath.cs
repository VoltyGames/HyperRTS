using System;
using HyperRTS.Simulation.Resources;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>A starting resource as a palette file writes it: the resource type's asset path and the amount.</summary>
    [Serializable]
    public sealed class MapResourcePath
    {
        [MapAsset(typeof(ResourceType))]
        public string type;
        public int amount;
    }
}
