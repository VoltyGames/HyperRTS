using System;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The selected feature, shared by the Map Editor's list, canvas and inspector.</summary>
    public sealed class MapFeatureSelection
    {
        public event Action Changed;

        public MapFeatureRef Current { get; private set; }

        public void Set(MapFeatureRef feature)
        {
            if (feature.Equals(Current))
            {
                return;
            }

            Current = feature;
            Changed?.Invoke();
        }

        public void Clear() => Set(MapFeatureRef.None);

        /// <summary>Drops the selection when a reload removed the feature.</summary>
        public void Validate(MapSpec spec)
        {
            if (!Current.IsNone && Current.Index >= Current.Kind.Count(spec))
            {
                Clear();
            }
        }
    }
}
