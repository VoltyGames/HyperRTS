using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>The Map Editor's left panel: every feature grouped by kind with counts; a click selects it.</summary>
    [UxmlElement]
    public sealed partial class MapFeatureList : TreeView
    {
        private const string GroupClass = "hrts-map-list__group";

        private readonly HashSet<string> _collapsed = new();
        private readonly Dictionary<int, MapFeatureKind> _groups = new();
        private readonly Dictionary<MapFeatureRef, int> _ids = new();
        private MapFeatureSelection _selection;
        private MapSpec _spec;
        private string _shape;

        public MapFeatureList()
        {
            makeItem = MakeItem;
            bindItem = BindItem;
            selectionChanged += _ => Pick();
            itemExpandedChanged += Remember;
        }

        /// <summary>Sets the selection that clicks write and rows show; the window binds it once.</summary>
        public void Bind(MapFeatureSelection selection) => _selection = selection;

        /// <summary>Rebuilds when features were added or removed; otherwise just renames the rows.</summary>
        public void Refresh(MapSpec spec)
        {
            _spec = spec;
            var shape = string.Join(",", MapFeatureKinds.All.Select(kind => kind.Count(spec)));
            if (shape != _shape)
            {
                _shape = shape;
                Fill(spec);
            }
            else
            {
                RefreshItems();
            }

            ShowSelection();
        }

        public void ShowSelection()
        {
            if (_ids.TryGetValue(_selection.Current, out var id))
            {
                SetSelectionByIdWithoutNotify(new[] { id });
                return;
            }

            SetSelectionWithoutNotify(Array.Empty<int>());
        }

        // A group's row is its kind with no index.
        private void Fill(MapSpec spec)
        {
            var id = 0;
            var roots = new List<TreeViewItemData<MapFeatureRef>>();
            _groups.Clear();
            _ids.Clear();
            // Spawns and other points first: the kinds drawn last are the ones most often picked.
            foreach (var kind in MapFeatureKinds.All.Reverse())
            {
                var features = new List<TreeViewItemData<MapFeatureRef>>();
                for (var i = 0; i < kind.Count(spec); i++)
                {
                    var feature = new MapFeatureRef(kind, i);
                    _ids[feature] = id;
                    features.Add(new TreeViewItemData<MapFeatureRef>(id++, feature));
                }

                _groups[id] = kind;
                roots.Add(new TreeViewItemData<MapFeatureRef>(id++, new MapFeatureRef(kind, -1), features));
            }

            SetRootItems(roots);
            Rebuild();
            foreach (var group in _groups.Where(group => !_collapsed.Contains(group.Value.Field)).Select(group => group.Key))
            {
                ExpandItem(group, false, false);
            }

            RefreshItems();
        }

        private static VisualElement MakeItem()
        {
            var item = new Label();
            item.AddToClassList("hrts-map-list__item");
            return item;
        }

        private void BindItem(VisualElement item, int index)
        {
            var feature = GetItemDataForIndex<MapFeatureRef>(index);
            var group = feature.Index < 0;
            item.EnableInClassList(GroupClass, group);
            ((Label)item).text = group
                ? $"{feature.Kind.Label} ({feature.Kind.Count(_spec)})"
                : feature.Kind.Describe(_spec, feature.Index);
        }

        private void Pick()
        {
            if (selectedItem is MapFeatureRef { Index: >= 0 } feature)
            {
                _selection.Set(feature);
            }
        }

        private void Remember(TreeViewExpansionChangedArgs change)
        {
            if (!_groups.TryGetValue(change.id, out var kind))
            {
                return;
            }

            if (change.isExpanded)
            {
                _collapsed.Remove(kind.Field);
            }
            else
            {
                _collapsed.Add(kind.Field);
            }
        }
    }
}
