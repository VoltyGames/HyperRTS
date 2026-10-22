using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// HyperRTS ▸ Map Editor: a read-only view of a map spec on a top-down canvas, with the feature list on the left
    /// and the selected feature's spec path on the right. Specs are edited as JSON; the view follows the file on disk.
    /// Double-clicking a <c>.map.json</c> opens it here.
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Map Editor";
        private const long DiskPollMilliseconds = 1000;

        [SerializeField] private string _specPath;

        private readonly MapFeatureSelection _selection = new();
        private MapSpecDocument _document;
        private MapEditorToolbar _toolbar;
        private Label _hint;
        private VisualElement _body;
        private MapFeatureList _list;
        private MapCanvas _canvas;
        private MapFeatureInspector _inspector;
        private MapReportPanel _report;
        private MapEditorPreview _preview;

        [MenuItem(EditorMenu.MapEditor, false, EditorMenu.MapEditorPriority)]
        public static void Open() => GetWindow<MapEditorWindow>(WindowTitle);

        public static void Open(string specPath) => GetWindow<MapEditorWindow>(WindowTitle).Load(specPath);

        [OnOpenAsset]
        private static bool OnOpenAsset(EntityId entityId, int line)
        {
            var path = AssetDatabase.GetAssetPath(EditorUtility.EntityIdToObject(entityId));
            if (string.IsNullOrEmpty(path) || !MapPaths.IsSpec(path))
            {
                return false;
            }

            Open(path);
            return true;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(MapEditorWindow)).CloneTree(root);
            _toolbar = root.Require<MapEditorToolbar>("toolbar");
            _toolbar.SpecPicked += Load;
            _toolbar.GhostsToggled += OnGhostsToggled;
            _toolbar.Strip.PreviewClicked += () => _preview?.Preview();
            _toolbar.Strip.BuildClicked += () => _preview?.Build();
            _hint = root.Require<Label>("hint");
            _body = root.Require<VisualElement>("body");
            _list = root.Require<MapFeatureList>("features");
            _list.Bind(_selection);
            _canvas = root.Require<MapCanvas>("canvas");
            _canvas.ShowGhosts = _toolbar.Ghosts;
            _inspector = root.Require<MapFeatureInspector>("inspector");
            _report = root.Require<MapReportPanel>("report");
            _report.ShowFeature += feature => _preview?.Show(feature);
            root.schedule.Execute(PollDisk).Every(DiskPollMilliseconds);

            if (_document == null && File.Exists(_specPath))
            {
                _document = MapSpecDocument.Open(_specPath);
            }

            ShowDocument();
        }

        private void OnEnable() => _selection.Changed += OnSelectionChanged;

        private void OnDisable()
        {
            _selection.Changed -= OnSelectionChanged;
            _preview?.Dispose();
            _preview = null;
            if (_document != null)
            {
                _document.Changed -= OnDocumentChanged;
            }
        }

        private void Load(string specPath)
        {
            Focus();
            if (_document != null && _document.Path == specPath)
            {
                return;
            }

            if (_document != null)
            {
                _document.Changed -= OnDocumentChanged;
            }

            _document = MapSpecDocument.Open(specPath);
            _specPath = specPath;
            _selection.Clear();
            ShowDocument();
        }

        private void ShowDocument()
        {
            if (_body == null)
            {
                return;
            }

            _preview?.Dispose();
            _preview = null;
            _hint.SetHidden(_document != null);
            _body.SetHidden(_document == null);
            if (_document == null)
            {
                return;
            }

            _canvas.Bind(_document, _selection);
            _preview = new MapEditorPreview(_document, _canvas, _selection, _toolbar.Strip, _report);
            _document.Changed -= OnDocumentChanged;
            _document.Changed += OnDocumentChanged;
            OnDocumentChanged();
            OnSelectionChanged();
        }

        private void OnDocumentChanged()
        {
            if (_document == null)
            {
                return;
            }

            _selection.Validate(_document.Spec);
            _list.Refresh(_document.Spec);
            _canvas.Refresh();
            _toolbar.Show(_document);
        }

        private void OnSelectionChanged()
        {
            if (_document == null)
            {
                return;
            }

            _list.ShowSelection();
            _inspector.Show(_selection.Current);
            _canvas.Refresh();
        }

        private void OnGhostsToggled(bool on)
        {
            _canvas.ShowGhosts = on;
        }

        private void PollDisk() => _document?.ReloadIfChangedOnDisk();
    }
}
