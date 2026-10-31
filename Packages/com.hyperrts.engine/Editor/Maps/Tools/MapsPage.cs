using System;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Designer;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Presentation.HUD;
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
        private HelpBox _none;
        private VisualElement _specs;
        private VisualTreeAsset _row;

        public override string Title => "Maps";

        public override int Order => 10;

        public override VisualElement Build()
        {
            var root = EditorAssets.Layout(typeof(MapsPage)).Instantiate();
            root.Require<ToolbarButton>("build-all").clicked += BuildAll;
            root.Require<ToolbarButton>("new-scene").clicked += RTSSceneWizard.Open;
            _none = root.Require<HelpBox>("none");
            _specs = root.Require<VisualElement>("specs");
            _row = EditorAssets.Layout(typeof(MapsPage), "Row");

            EditorApplication.projectChanged += ShowSpecs;
            ShowSpecs();
            return root;
        }

        public override void Release()
        {
            EditorApplication.projectChanged -= ShowSpecs;
            _specs = null;
        }

        private void ShowSpecs()
        {
            _specs.Clear();
            var specs = MapPaths.All();
            _none.SetHidden(specs.Length > 0);
            foreach (var spec in specs)
            {
                _specs.Add(SpecRow(spec));
            }
        }

        private VisualElement SpecRow(string specPath)
        {
            var row = _row.CloneRoot();
            row.tooltip = specPath;
            var map = MapPaths.MapName(specPath);
            row.Require<Label>("name").text = map;
            row.Require<Label>("info").text = Summary(specPath);
            row.Require<Label>("status").text = ReportStatus(map);
            row.Require<Button>("open").clicked += () => MapEditorWindow.Open(specPath);
            row.Require<Button>("build").clicked += () => BuildOne(specPath);
            return row;
        }

        /// <summary>Mode and size from the spec, or why it doesn't read.</summary>
        private static string Summary(string specPath)
        {
            try
            {
                var spec = MapSpecJson.Read(File.ReadAllText(specPath));
                return $"{spec.mode}  {spec.size.x:0}×{spec.size.z:0} m";
            }
            catch (InvalidOperationException exception)
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
            MapBuildRunner.Build(specPath);
            ShowSpecs();
        }

        private void BuildAll()
        {
            MapBuildRunner.BuildAll();
            ShowSpecs();
        }
    }
}
