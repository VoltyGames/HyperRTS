using System;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.Maps.Spec;
using HyperRTS.Editor.Maps.Tools;
using HyperRTS.Editor.Scenes;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer.Window
{
    /// <summary>
    /// The Map Editor's landing view: a card per map spec in the project with its overview picture, mode, size and
    /// the last build's issue counts, marked Out of date when the spec was saved since. Cards open the spec, open the
    /// built scene or build it; the toolbar builds every map or opens the New Blank Scene dialog.
    /// </summary>
    [UxmlElement]
    public sealed partial class MapGallery : VisualElement
    {
        private readonly VisualTreeAsset _card = EditorAssets.Layout(typeof(MapGallery), "Card");
        private readonly VisualElement _cards;
        private readonly Label _empty;

        public MapGallery()
        {
            EditorAssets.Layout(typeof(MapGallery)).CloneTree(this);
            this.Require<ToolbarButton>("build-all").clicked += BuildAll;
            this.Require<ToolbarButton>("new-scene").clicked += SceneWizard.Open;
            _cards = this.Require<VisualElement>("cards");
            _empty = this.Require<Label>("empty");
        }

        /// <summary>A card's Open was clicked; the argument is the spec path.</summary>
        public event Action<string> OpenRequested;

        /// <summary>Rebuilds the cards from the specs and reports now on disk.</summary>
        public void Refresh()
        {
            _cards.Clear();
            var specs = MapPaths.All();
            _empty.SetHidden(specs.Length > 0);
            foreach (var spec in specs)
            {
                _cards.Add(CreateCard(spec));
            }
        }

        private VisualElement CreateCard(string specPath)
        {
            var paths = new MapPaths(specPath, MapPaths.MapName(specPath));
            var report = MapReport.Load(paths.Name);
            var card = _card.CloneRoot();
            card.tooltip = specPath;
            var overview = card.Require<Image>("overview");
            overview.scaleMode = ScaleMode.ScaleAndCrop;
            overview.image = AssetDatabase.LoadAssetAtPath<Texture2D>(paths.Overview);
            card.Require<Label>("name").text = paths.Name;
            card.Require<Label>("stale").SetHidden(!paths.OutOfDate);
            card.Require<Label>("info").text = Info(specPath, report);
            ShowIssues(card.Require<Label>("issues"), report);
            card.Require<Button>("open").clicked += () => OpenRequested?.Invoke(specPath);
            var openScene = card.Require<Button>("open-scene");
            openScene.SetEnabled(File.Exists(paths.Scene));
            openScene.clicked += () => paths.OpenScene();
            card.Require<Button>("build").clicked += () => Build(specPath);
            return card;
        }

        /// <summary>Mode and size from the spec, or why it doesn't read, then when it was built.</summary>
        private static string Info(string specPath, MapReport report)
        {
            var built = report == null ? "not built" : "built " + report.built;
            try
            {
                var spec = MapSpecJson.Read(File.ReadAllText(specPath));
                return $"{spec.mode}, {spec.size.x:0} x {spec.size.z:0} m, {built}";
            }
            catch (InvalidOperationException exception)
            {
                return "Unreadable: " + exception.Message;
            }
        }

        private static void ShowIssues(Label label, MapReport report)
        {
            label.EnableInClassList("hrts-map-card__issues--error", report != null && report.errors.Count > 0);
            label.EnableInClassList("hrts-map-card__issues--warning",
                report != null && report.errors.Count == 0 && report.warnings.Count > 0);
            if (report == null)
            {
                label.text = "No report yet";
                return;
            }

            label.text = report.Passed && report.warnings.Count == 0
                ? "Passed"
                : report.Counts;
        }

        private void Build(string specPath)
        {
            MapBuildRunner.Build(specPath);
            Refresh();
        }

        private void BuildAll()
        {
            MapBuildRunner.BuildAll();
            Refresh();
        }
    }
}
