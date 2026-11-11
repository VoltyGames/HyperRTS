using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Icons;
using HyperRTS.Editor.Maps.Designer.Canvas;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Presentation.HUD;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer.Features
{
    /// <summary>
    /// The Map Editor's layers panel: every feature grouped by category, each with its glyph in the canvas colour,
    /// and per category an eye and a lock (<see cref="MapLayers"/>). A search field filters the features; a click
    /// selects one.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapFeatureList : VisualElement
    {
        private const string HiddenClass = "hrts-map-layer--hidden";
        private const string DimClass = "hrts-map-layer__toggle--dim";
        private const float RowHeight = 22f;

        private readonly VisualTreeAsset _item = EditorAssets.Layout(typeof(MapFeatureList), "Item");
        private readonly ToolbarSearchField _search;
        private readonly TreeView _tree;
        private readonly HashSet<string> _collapsed = new();
        private readonly Dictionary<int, MapFeatureKind> _groups = new();
        private readonly Dictionary<MapFeatureRef, int> _ids = new();
        private MapFeatureSelection _selection;
        private MapLayers _layers;
        private MapSpec _spec;
        private string _shape;

        public MapFeatureList()
        {
            EditorAssets.Layout(typeof(MapFeatureList)).CloneTree(this);
            _search = this.Require<ToolbarSearchField>("search");
            _search.RegisterValueChangedCallback(_ => Refresh(_spec));
            _tree = this.Require<TreeView>("tree");
            _tree.fixedItemHeight = RowHeight;
            _tree.makeItem = () => _item.CloneRoot();
            _tree.bindItem = BindItem;
            _tree.selectionChanged += _ => Pick();
            _tree.itemExpandedChanged += Remember;
        }

        private string Filter => _search.value?.Trim() ?? string.Empty;

        /// <summary>Sets the selection that clicks write and the layers the eye and lock toggles write; the window binds them once.</summary>
        public void Bind(MapFeatureSelection selection, MapLayers layers)
        {
            _selection = selection;
            _layers = layers;
            _layers.Changed += _tree.RefreshItems;
        }

        /// <summary>Rebuilds when features were added, removed or filtered in or out; otherwise just relabels the rows.</summary>
        public void Refresh(MapSpec spec)
        {
            if (spec == null)
            {
                return;
            }

            _spec = spec;
            var shape = Shape(spec);
            if (shape != _shape)
            {
                _shape = shape;
                Fill(spec);
            }
            else
            {
                _tree.RefreshItems();
            }

            ShowSelection();
        }

        public void ShowSelection()
        {
            if (_ids.TryGetValue(_selection.Current, out var id))
            {
                _tree.SetSelectionByIdWithoutNotify(new[] { id });
                return;
            }

            _tree.SetSelectionWithoutNotify(Array.Empty<int>());
        }

        // Counts per category, and while filtering, which features match.
        private string Shape(MapSpec spec)
        {
            var counts = string.Join(",", MapFeatureKinds.All.Select(kind => kind.Count(spec)));
            var filter = Filter;
            if (filter.Length == 0)
            {
                return counts;
            }

            var matches = MapFeatureKinds.All.SelectMany(kind => Matches(spec, kind).Select(i => $"{kind.Field}{i}"));
            return $"{counts}|{filter}|{string.Join(",", matches)}";
        }

        private IEnumerable<int> Matches(MapSpec spec, MapFeatureKind kind)
        {
            var filter = Filter;
            for (var i = 0; i < kind.Count(spec); i++)
            {
                if (filter.Length == 0 || kind.Describe(spec, i).Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    yield return i;
                }
            }
        }

        // A group's row is its category with no index.
        private void Fill(MapSpec spec)
        {
            var id = 0;
            var filtering = Filter.Length > 0;
            var roots = new List<TreeViewItemData<MapFeatureRef>>();
            _groups.Clear();
            _ids.Clear();
            // Spawns and other points first: the kinds drawn last are the ones most often picked.
            foreach (var kind in MapFeatureKinds.All.Reverse())
            {
                var features = new List<TreeViewItemData<MapFeatureRef>>();
                foreach (var index in Matches(spec, kind))
                {
                    var feature = new MapFeatureRef(kind, index);
                    _ids[feature] = id;
                    features.Add(new TreeViewItemData<MapFeatureRef>(id++, feature));
                }

                if (filtering && features.Count == 0)
                {
                    continue;
                }

                _groups[id] = kind;
                roots.Add(new TreeViewItemData<MapFeatureRef>(id++, new MapFeatureRef(kind, -1), features));
            }

            _tree.SetRootItems(roots);
            _tree.Rebuild();
            foreach (var group in _groups.Where(group => filtering || !_collapsed.Contains(group.Value.Field)))
            {
                _tree.ExpandItem(group.Key, false, false);
            }

            _tree.RefreshItems();
        }

        private void BindItem(VisualElement item, int index)
        {
            var feature = _tree.GetItemDataForIndex<MapFeatureRef>(index);
            var kind = feature.Kind;
            var group = feature.Index < 0;
            var hidden = !_layers.IsVisible(kind);
            item.EnableInClassList(HiddenClass, hidden);
            item.Require<VisualElement>("glyph").SetGlyph(kind.Glyph).style.unityBackgroundImageTintColor = kind.Color;
            var label = item.Require<Label>("label");
            label.text = group ? $"{kind.Label} ({kind.Count(_spec)})" : kind.Describe(_spec, feature.Index);
            label.EnableInClassList("hrts-map-layer__label--group", group);

            var show = item.Require<VisualElement>("show");
            var padlock = item.Require<VisualElement>("lock");
            show.SetHidden(!group);
            padlock.SetHidden(!group);
            if (!group)
            {
                return;
            }

            var locked = _layers.IsLocked(kind);
            show.SetGlyph(hidden ? "eye-off" : "eye").EnableInClassList(DimClass, hidden);
            padlock.SetGlyph(locked ? "lock" : "lock-open").EnableInClassList(DimClass, !locked);
            show.userData = kind;
            padlock.userData = kind;
            Listen(show, ToggleShown);
            Listen(padlock, ToggleLocked);
        }

        // Rows are recycled, so each bind swaps the click handler rather than stacking another.
        private static void Listen(VisualElement element, EventCallback<ClickEvent> callback)
        {
            element.UnregisterCallback(callback);
            element.RegisterCallback(callback);
        }

        private void ToggleShown(ClickEvent evt)
        {
            var kind = (MapFeatureKind)((VisualElement)evt.currentTarget).userData;
            _layers.SetVisible(kind, !_layers.IsVisible(kind));
            evt.StopPropagation();
        }

        private void ToggleLocked(ClickEvent evt)
        {
            var kind = (MapFeatureKind)((VisualElement)evt.currentTarget).userData;
            _layers.SetLocked(kind, !_layers.IsLocked(kind));
            evt.StopPropagation();
        }

        private void Pick()
        {
            if (_tree.selectedItem is MapFeatureRef { Index: >= 0 } feature)
            {
                _selection.Set(feature);
            }
        }

        private void Remember(TreeViewExpansionChangedArgs change)
        {
            if (!_groups.TryGetValue(change.id, out var kind) || Filter.Length > 0)
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
