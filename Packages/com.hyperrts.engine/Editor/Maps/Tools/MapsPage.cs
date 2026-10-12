using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Tools ▸ Maps: every map spec in the project with a Build button, Build all, and a new blank scene.</summary>
    public class MapsPage : HubPage
    {
        private ScrollView _list;

        public override string Title => "Maps";

        public override int Order => 10;

        public override VisualElement Build()
        {
            var root = new VisualElement();
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(BuildAll) { text = "Build all" });
            toolbar.Add(new ToolbarButton(RTSSceneWizard.Open) { text = "New blank scene..." });
            toolbar.Add(new ToolbarSpacer { flex = true });
            root.Add(toolbar);

            _list = new ScrollView();
            _list.AddToClassList("hrts-fill");
            _list.contentContainer.AddToClassList("hrts-page__body");
            root.Add(_list);

            EditorApplication.projectChanged += ShowSpecs;
            ShowSpecs();
            return root;
        }

        public override void Release()
        {
            EditorApplication.projectChanged -= ShowSpecs;
            _list = null;
        }

        private void ShowSpecs()
        {
            _list.Clear();
            var specs = MapPaths.All();
            if (specs.Length == 0)
            {
                var none = new HelpBox("No map specs (*.map.json) in the project.", HelpBoxMessageType.Info);
                none.AddToClassList("hrts-note");
                _list.Add(none);
                return;
            }

            foreach (var spec in specs)
            {
                _list.Add(SpecRow(spec));
            }
        }

        private static VisualElement SpecRow(string spec)
        {
            var row = new VisualElement { tooltip = spec };
            row.AddToClassList("hrts-row");
            var name = new Label(MapPaths.NameOf(spec));
            name.AddToClassList("hrts-map__name");
            row.Add(name);
            var build = new Button(() => MapMenu.BuildSpec(spec)) { text = "Build" };
            build.AddToClassList("hrts-map__build");
            row.Add(build);
            return row;
        }

        private static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            foreach (var spec in MapPaths.All())
            {
                MapBuilder.Build(spec);
            }
        }
    }
}
