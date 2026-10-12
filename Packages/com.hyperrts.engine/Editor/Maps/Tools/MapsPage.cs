using System;
using System.IO;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>
    /// Tools ▸ Maps: every map spec in the project with its mode, size and last build's result, and buttons to open
    /// it in the Map Editor or build it; New map, Build all and a new blank scene.
    /// </summary>
    public class MapsPage : HubPage
    {
        private ScrollView _list;

        public override string Title => "Maps";

        public override int Order => 10;

        public override VisualElement Build()
        {
            var root = new VisualElement();
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(NewMapDialog.Open) { text = "New map..." });
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
                var none = new HelpBox("No map specs (*.map.json) in the project. New map... starts one.",
                    HelpBoxMessageType.Info);
                none.AddToClassList("hrts-note");
                _list.Add(none);
                return;
            }

            foreach (var spec in specs)
            {
                _list.Add(SpecRow(spec));
            }
        }

        private VisualElement SpecRow(string specPath)
        {
            var row = new VisualElement { tooltip = specPath };
            row.AddToClassList("hrts-row");
            var map = MapPaths.NameOf(specPath);
            row.Add(Cell(map, "hrts-map__name"));
            row.Add(Cell(Summary(specPath), "hrts-map__info"));
            row.Add(Cell(ReportStatus(map), "hrts-map__status"));
            var open = new Button(() => MapEditorWindow.Open(specPath)) { text = "Open" };
            open.AddToClassList("hrts-map__build");
            row.Add(open);
            var build = new Button(() => BuildOne(specPath)) { text = "Build" };
            build.AddToClassList("hrts-map__build");
            row.Add(build);
            return row;
        }

        private static Label Cell(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        /// <summary>Mode and size from the spec, or why it doesn't read.</summary>
        private static string Summary(string specPath)
        {
            try
            {
                var spec = MapSpecJson.Read(File.ReadAllText(specPath));
                return $"{spec.mode}  {spec.size.x:0}×{spec.size.z:0} m";
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            {
                return "Unreadable: " + exception.Message;
            }
        }

        /// <summary>The last build's errors and warnings from Logs/MapReports, or that it was never built.</summary>
        private static string ReportStatus(string map)
        {
            var report = MapReport.Load(map);
            if (report == null)
            {
                return "not built";
            }

            return report.Passed && report.warnings.Count == 0
                ? "passed"
                : $"{report.errors.Count} errors, {report.warnings.Count} warnings";
        }

        private void BuildOne(string specPath)
        {
            MapBuildCommands.Build(specPath);
            ShowSpecs();
        }

        private void BuildAll()
        {
            MapBuildCommands.BuildAll();
            ShowSpecs();
        }
    }
}
