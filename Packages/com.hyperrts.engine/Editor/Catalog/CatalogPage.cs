using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>Tools ▸ Catalog: every unit and building prefab in one editable stats table, plus the tech tree.</summary>
    public class CatalogPage : HubPage
    {
        // Asset changes arrive in bursts; one project walk after they settle is enough.
        private const long ReloadDelayMilliseconds = 500;

        private List<GameEntityAuthoring> _prefabs = new();
        private string _filter = "";
        private bool _reloadPending;
        private bool _treePending;
        private VisualElement _root;
        private StatsTable _stats;
        private TechTreeView _techTree;
        private TabView _tabs;
        private Tab _treeTab;

        public override string Title => "Catalog";

        public override int Order => 20;

        public override VisualElement Build()
        {
            _root = EditorAssets.Layout(typeof(CatalogPage)).Instantiate();
            var search = _root.Require<ToolbarSearchField>("search");
            search.SetValueWithoutNotify(_filter);
            search.RegisterValueChangedCallback(change =>
            {
                _filter = change.newValue;
                ShowPrefabs();
            });

            _stats = _root.Require<StatsTable>("stats");
            _techTree = _root.Require<TechTreeView>("tree");
            _tabs = _root.Require<TabView>("tabs");
            _treeTab = _root.Require<Tab>("tree-tab");
            _tabs.activeTabChanged += (_, _) => ShowPrefabs();

            EditorApplication.projectChanged += OnProjectChange;
            ObjectChangeEvents.changesPublished += OnObjectsChanged;
            Reload();
            return _root;
        }

        public override void Release()
        {
            EditorApplication.projectChanged -= OnProjectChange;
            ObjectChangeEvents.changesPublished -= OnObjectsChanged;
            _stats?.Release();
            _root = null;
            _tabs = null;
            _reloadPending = false;
            _treePending = false;
        }

        private void OnProjectChange()
        {
            if (_reloadPending || _tabs == null)
            {
                return;
            }

            _reloadPending = true;
            _root.schedule.Execute(Reload).ExecuteLater(ReloadDelayMilliseconds);
        }

        // Inspector edits to options or prerequisites show up in the open tree, once a burst of edits (a drag) settles.
        private void OnObjectsChanged(ref ObjectChangeEventStream stream)
        {
            if (_treePending || _tabs == null || _tabs.activeTab != _treeTab)
            {
                return;
            }

            _treePending = true;
            _root.schedule.Execute(RefreshTree).ExecuteLater(ReloadDelayMilliseconds);
        }

        private void RefreshTree()
        {
            _treePending = false;
            if (_tabs.activeTab == _treeTab)
            {
                _techTree.Show(_prefabs, Visible());
            }
        }

        private void Reload()
        {
            _reloadPending = false;
            _prefabs = EditorAssets.EntityPrefabs()
                .OrderBy(prefab => prefab is BuildingAuthoring)
                .ThenBy(prefab => prefab.DisplayName)
                .ToList();
            ShowPrefabs();
        }

        private void ShowPrefabs()
        {
            if (_tabs.activeTab == _treeTab)
            {
                _techTree.Show(_prefabs, Visible());
                return;
            }

            _stats.Show(Visible());
        }

        private List<GameEntityAuthoring> Visible() => _prefabs.Where(MatchesFilter).ToList();

        private bool MatchesFilter(GameEntityAuthoring prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            return prefab.DisplayName.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
