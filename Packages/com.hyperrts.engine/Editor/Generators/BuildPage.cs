using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>Tools ▸ Build: one tab per <see cref="BuildTab"/>, such as the generators, art commands and player builds.</summary>
    public sealed class BuildPage : HubPage
    {
        public override string Title => "Build";

        public override string Glyph => "hammer";

        public override int Order => 90;

        public override VisualElement Build()
        {
            var root = Layout().Instantiate();
            var tabs = root.Require<TabView>("tabs");
            var buildTabs = TypeDiscovery.Instances<BuildTab>().OrderBy(tab => tab.Order).ToArray();
            foreach (var buildTab in buildTabs)
            {
                tabs.Add(new Tab(buildTab.Title) { userData = buildTab });
            }

            var saved = EditorState.instance.buildTab;
            var chosen = tabs.Children().OfType<Tab>().FirstOrDefault(tab => tab.userData.GetType().FullName == saved);
            if (chosen != null)
            {
                tabs.activeTab = chosen;
            }

            Show(tabs.activeTab);
            tabs.activeTabChanged += Switch;
            return root;
        }

        // A hidden tab drops its content, which stops its polling.
        private static void Switch(Tab previous, Tab next)
        {
            previous?.contentContainer.Clear();
            Show(next);
        }

        private static void Show(Tab tab)
        {
            if (tab == null || tab.contentContainer.childCount > 0)
            {
                return;
            }

            var buildTab = (BuildTab)tab.userData;
            EditorState.instance.buildTab = buildTab.GetType().FullName;
            EditorState.instance.Save();
            var content = buildTab.Build();
            content.AddToClassList("hrts-fill");
            tab.Add(content);
        }
    }
}
