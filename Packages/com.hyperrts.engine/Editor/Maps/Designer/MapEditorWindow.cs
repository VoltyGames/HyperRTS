using System;
using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// The Map Editor window. It opens on a gallery of the project's maps; an opened spec shows on a top-down canvas
    /// with a layers panel and, on the right, the selected feature, the issues and the report. Every edit saves at
    /// once, and double-clicking a <c>.map.json</c> opens it.
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Map Editor";
        private const double WarningSeconds = 5.0;
        private const long GalleryRefreshMilliseconds = 500;

        [SerializeField] private string _specPath;

        // Kept across script reloads with its undo history; destroyed with the window.
        [SerializeField] private MapSpecDocument _document;

        private readonly MapFeatureSelection _selection = new();
        private readonly MapToolbox _toolbox = new();
        private readonly MapLayers _layers = new();
        private MapGallery _gallery;
        private IVisualElementScheduledItem _galleryRefresh;
        private VisualElement _editor;
        private MapEditorToolbar _toolbar;
        private MapFeatureList _list;
        private MapCanvas _canvas;
        private MapStatusBar _status;
        private MapFeatureInspector _inspector;
        private MapIssueList _issues;
        private Tab _issuesTab;
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
            _gallery = root.Require<MapGallery>("gallery");
            _gallery.OpenRequested += Load;
            _editor = root.Require<VisualElement>("editor");
            _toolbar = root.Require<MapEditorToolbar>("toolbar");
            _toolbar.GalleryClicked += ShowGallery;
            _toolbar.Tools.Bind(_toolbox);
            _toolbar.GhostsToggled += on => _canvas.ShowGhosts = on;
            _toolbar.Strip.PreviewClicked += () => _preview?.Preview();
            _toolbar.Strip.BuildClicked += () => _preview?.Build();
            root.Require<MapToolOptions>("options").Bind(_toolbox);
            _list = root.Require<MapFeatureList>("features");
            _list.Bind(_selection, _layers);
            _canvas = root.Require<MapCanvas>("canvas");
            _canvas.ShowGhosts = _toolbar.Ghosts;
            _canvas.Warned += message => ShowNotification(new GUIContent(message), WarningSeconds);
            _status = root.Require<MapStatusBar>("status");
            _canvas.CursorMoved += world => _status.ShowCursor(world);
            _canvas.ViewChanged += () => _status.ShowView(_canvas.Zoom, _canvas.GridMetres);
            _inspector = root.Require<MapFeatureInspector>("inspector");
            _issues = root.Require<MapIssueList>("issues");
            _issuesTab = root.Require<Tab>("issues-tab");
            _issues.Changed += ShowIssueCount;
            _issues.Picked += (feature, error) => _preview?.Show(feature, error);
            _report = root.Require<MapReportPanel>("report");

            if (_document == null && File.Exists(_specPath))
            {
                _document = MapSpecDocument.Open(_specPath);
            }

            ShowDocument();
        }

        private void OnEnable()
        {
            _selection.Changed += OnSelectionChanged;
            _layers.Changed += OnLayersChanged;
        }

        private void OnProjectChange()
        {
            // A map build writes many assets, so the gallery refreshes once their changes have stopped for a moment.
            if (_document == null && _gallery != null)
            {
                (_galleryRefresh ??= _gallery.schedule.Execute(_gallery.Refresh)).ExecuteLater(GalleryRefreshMilliseconds);
            }
        }

        private void OnDisable()
        {
            _selection.Changed -= OnSelectionChanged;
            _layers.Changed -= OnLayersChanged;
            _preview?.Dispose();
            _preview = null;
            if (_document != null)
            {
                _document.Changed -= OnDocumentChanged;
            }
        }

        private void OnDestroy() => DestroyImmediate(_document);

        private void Load(string specPath)
        {
            Focus();
            if (_document != null && _document.Path == specPath)
            {
                return;
            }

            CloseDocument();
            _document = MapSpecDocument.Open(specPath);
            _specPath = specPath;
            ShowDocument();
        }

        private void ShowGallery()
        {
            CloseDocument();
            _specPath = null;
            ShowDocument();
        }

        private void CloseDocument()
        {
            if (_document != null)
            {
                _document.Changed -= OnDocumentChanged;
                DestroyImmediate(_document);
                _document = null;
            }

            _selection.Clear();
        }

        private void ShowDocument()
        {
            if (_editor == null)
            {
                return;
            }

            _preview?.Dispose();
            _preview = null;
            _gallery.SetHidden(_document != null);
            _editor.SetHidden(_document == null);
            if (_document == null)
            {
                _gallery.Refresh();
                return;
            }

            _status.ShowPreviewed(null);
            _canvas.Bind(_document, _selection, _toolbox, _layers);
            _preview = new MapEditorPreview(_document, _canvas, _selection, _toolbar.Strip, _report, _issues);
            _preview.Previewed += () => _status.ShowPreviewed(DateTime.Now);
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
            _inspector.Show(_document, _selection.Current);
            _canvas.ClearMark();
            _canvas.Refresh();
        }

        // A category the canvas no longer picks cannot keep its feature selected.
        private void OnLayersChanged()
        {
            var current = _selection.Current;
            if (!current.IsNone && !_layers.CanPick(current.Kind))
            {
                _selection.Clear();
            }

            if (_document != null)
            {
                _canvas.Refresh();
            }
        }

        private void ShowIssueCount() =>
            _issuesTab.label = _issues.Count == 0 ? "Issues" : $"Issues ({_issues.Count})";
    }
}
