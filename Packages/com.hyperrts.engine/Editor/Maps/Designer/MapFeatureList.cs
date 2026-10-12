using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's left panel: every feature grouped by kind with counts; a click selects it.</summary>
    public sealed class MapFeatureList : ScrollView
    {
        private const string ItemClass = "hrts-map-list__item";
        private const string SelectedClass = "hrts-map-list__item--selected";

        private readonly MapFeatureSelection _selection;
        private readonly Dictionary<MapFeatureRef, Label> _items = new();
        private readonly HashSet<string> _collapsed = new();
        private string _shape;

        public MapFeatureList(MapFeatureSelection selection)
        {
            _selection = selection;
            AddToClassList("hrts-map-list");
        }

        /// <summary>Rebuilds when features were added or removed; otherwise just renames the rows.</summary>
        public void Refresh(MapSpec spec)
        {
            var shape = string.Join(",", MapFeatureKinds.All.Select(kind => kind.Count(spec)));
            if (shape != _shape)
            {
                _shape = shape;
                Rebuild(spec);
            }

            foreach (var (feature, label) in _items)
            {
                label.text = feature.Kind.Describe(spec, feature.Index);
            }

            ShowSelection();
        }

        public void ShowSelection()
        {
            foreach (var (feature, label) in _items)
            {
                label.EnableInClassList(SelectedClass, feature.Equals(_selection.Current));
            }
        }

        private void Rebuild(MapSpec spec)
        {
            Clear();
            _items.Clear();
            // Spawns and other points first: the kinds drawn last are the ones most often picked.
            foreach (var kind in MapFeatureKinds.All.Reverse())
            {
                Add(Group(spec, kind));
            }
        }

        private Foldout Group(MapSpec spec, MapFeatureKind kind)
        {
            var count = kind.Count(spec);
            var group = new Foldout { text = $"{kind.Label} ({count})", value = !_collapsed.Contains(kind.Field) };
            group.AddToClassList("hrts-map-list__group");
            group.RegisterValueChangedCallback(evt => Remember(kind, evt.newValue));
            for (var i = 0; i < count; i++)
            {
                var feature = new MapFeatureRef(kind, i);
                var item = new Label();
                item.AddToClassList(ItemClass);
                item.RegisterCallback<ClickEvent>(_ => _selection.Set(feature));
                _items[feature] = item;
                group.Add(item);
            }

            return group;
        }

        private void Remember(MapFeatureKind kind, bool open)
        {
            if (open)
            {
                _collapsed.Remove(kind.Field);
                return;
            }

            _collapsed.Add(kind.Field);
        }
    }
}
