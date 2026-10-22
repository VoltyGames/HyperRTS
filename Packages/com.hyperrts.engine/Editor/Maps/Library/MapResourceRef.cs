using System;
using HyperRTS.Simulation.Resources;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>A starting resource as a palette file writes it: the resource type's asset and the amount.</summary>
    [Serializable]
    public sealed class MapResourceRef
    {
        [MapAsset(typeof(ResourceType))]
        public MapAssetRef type = new();
        public int amount;
    }
}
