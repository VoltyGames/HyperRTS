using System;
using System.Linq;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>HyperRTS ▸ Tools: one dockable window with a sidebar of every <see cref="HubPage"/>.</summary>
    public class ToolsWindow : EditorWindow
    {
        private const string WindowTitle = "HyperRTS Tools";

        // SessionState survives domain reloads, so the window reopens on the page it showed.
        private const string LastPageKey = "HyperRTS.Tools.LastPage";

        private const long BadgeMilliseconds = 2000;

        private HubPage[] _pages = Array.Empty<HubPage>();
        private string[] _badges = Array.Empty<string>();
        private HubPage _current;
        private ListView _sidebar;
        private VisualElement _content;

        [MenuItem(EditorMenu.Tools, false, EditorMenu.ToolsPriority)]
        public static void Open() => GetWindow<ToolsWindow>(WindowTitle);

        /// <summary>Opens or focuses the window on the page of type <typeparamref name="TPage"/>.</summary>
        public static void Show<TPage>() where TPage : HubPage
        {
            SessionState.SetString(LastPageKey, typeof(TPage).FullName);
            var window = GetWindow<ToolsWindow>(WindowTitle);
            window.SelectSaved();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.AddStyles(root);
            _pages = TypeDiscovery.Instances<HubPage>().OrderBy(page => page.Order).ToArray();
            _badges = new string[_pages.Length];

            var split = new VisualElement();
            split.AddToClassList("hrts-tools");
            _sidebar = SidebarList.Create(_pages, _badges);
            _sidebar.selectedIndicesChanged += _ => ShowSelected();
            split.Add(_sidebar);

            _content = new VisualElement();
            _content.AddToClassList("hrts-page-host");
            split.Add(_content);
            root.Add(split);

            root.schedule.Execute(RefreshBadges).Every(BadgeMilliseconds);
            SelectSaved();
        }

        private void OnDisable() => ReleaseCurrent();

        private void OnHierarchyChange() => RefreshBadges();

        private void OnProjectChange() => RefreshBadges();

        private void SelectSaved()
        {
            if (_sidebar == null || _pages.Length == 0)
            {
                return;
            }

            var saved = SessionState.GetString(LastPageKey, "");
            var index = Array.FindIndex(_pages, page => page.GetType().FullName == saved);
            _sidebar.SetSelection(Math.Max(index, 0));
        }

        private void ShowSelected()
        {
            var index = _sidebar.selectedIndex;
            if (index < 0 || _pages[index] == _current)
            {
                return;
            }

            ReleaseCurrent();
            _current = _pages[index];
            SessionState.SetString(LastPageKey, _current.GetType().FullName);

            var page = _current.Build();
            page.AddToClassList("hrts-page");
            _content.Add(page);
            RefreshBadges();
        }

        private void ReleaseCurrent()
        {
            if (_current == null)
            {
                return;
            }

            _current.Release();
            _current = null;
            _content?.Clear();
        }

        // Rebinding only on a real change keeps the sidebar's hover and selection steady.
        private void RefreshBadges()
        {
            if (_sidebar == null)
            {
                return;
            }

            var changed = false;
            for (var i = 0; i < _pages.Length; i++)
            {
                var badge = _pages[i].Badge;
                if (badge != _badges[i])
                {
                    _badges[i] = badge;
                    changed = true;
                }
            }

            if (changed)
            {
                _sidebar.RefreshItems();
            }
        }
    }
}
