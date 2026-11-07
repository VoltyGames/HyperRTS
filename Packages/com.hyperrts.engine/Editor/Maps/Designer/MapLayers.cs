using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// Which feature categories the Map Editor canvas shows and lets you pick: a hidden one is not drawn or hit, a
    /// locked one is drawn but not hit. Kept for the window's life, not saved with the spec.
    /// </summary>
    public sealed class MapLayers
    {
        private readonly HashSet<string> _hidden = new();
        private readonly HashSet<string> _locked = new();

        public event Action Changed;

        public bool IsVisible(MapFeatureKind kind) => !_hidden.Contains(kind.Field);

        public bool IsLocked(MapFeatureKind kind) => _locked.Contains(kind.Field);

        /// <summary>True when a click on the canvas may select or edit the kind's features.</summary>
        public bool CanPick(MapFeatureKind kind) => IsVisible(kind) && !IsLocked(kind);

        public void SetVisible(MapFeatureKind kind, bool visible) => Set(_hidden, kind, !visible);

        public void SetLocked(MapFeatureKind kind, bool locked) => Set(_locked, kind, locked);

        private void Set(HashSet<string> set, MapFeatureKind kind, bool on)
        {
            var changed = on ? set.Add(kind.Field) : set.Remove(kind.Field);
            if (changed)
            {
                Changed?.Invoke();
            }
        }
    }
}
