using System;

namespace HyperRTS.Simulation.Common.Authoring
{
    /// <summary>
    /// A reference, or a list of them, that must point at a prefab asset: a scene object stops existing once the
    /// scene bakes. Validation offers to swap a list entry for the prefab it came from.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PrefabAssetAttribute : Attribute
    {
        public PrefabAssetAttribute(string label) => Label = label;

        /// <summary>How a warning names one entry, e.g. "Production option".</summary>
        public string Label { get; }
    }
}
