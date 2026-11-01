using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A spec feature the symmetry copies into every sector, or places once when <see cref="shared"/>.</summary>
    [Serializable]
    public abstract class MapFeatureSpec
    {
        /// <summary>Placed once instead of copied, so it must sit on the symmetry's axis or at its centre.</summary>
        public bool shared;
    }
}
