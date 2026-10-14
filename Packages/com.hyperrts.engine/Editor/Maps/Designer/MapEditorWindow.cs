using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
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
        private VisualElement _body;
        private MapFeatureList _list;
        private MapCanvas _canvas;
        private MapFeatureInspector _inspector;
        private MapPreviewStrip _previewStrip;
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
            EditorAssets.AddStyles(root);
            root.AddToClassList("hrts-map-editor");
            _toolbar = new MapEditorToolbar(Load, OnGhostsToggled);
            root.Add(_toolbar);
            _previewStrip = new MapPreviewStrip(() => _preview?.Preview(), () => _preview?.Build());
            _toolbar.Add(_previewStrip);
            _body = new VisualElement();
            _body.AddToClassList("hrts-map-editor__body");
            root.Add(_body);
            root.schedule.Execute(PollDisk).Every(DiskPollMilliseconds);

            if (_document == null && File.Exists(_specPath))
            {
                _document = MapSpecDocument.Open(_specPath);
            }

            BuildBody();
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
            BuildBody();
        }

        private void BuildBody()
        {
            if (_body == null)
            {
                return;
            }

            _body.Clear();
            _preview?.Dispose();
            _preview = null;
            if (_document == null)
            {
                var hint = new Label("Pick a map spec in the toolbar, or double-click a .map.json file.");
                hint.AddToClassList("hrts-hint");
                _body.Add(hint);
                return;
            }

            _list = new MapFeatureList(_selection);
            _canvas = new MapCanvas(_document, _selection) { ShowGhosts = _toolbar.Ghosts };
            _inspector = new MapFeatureInspector();
            _body.Add(_list);
            _body.Add(_canvas);
            _body.Add(SidePanel());

            _document.Changed -= OnDocumentChanged;
            _document.Changed += OnDocumentChanged;
            OnDocumentChanged();
            OnSelectionChanged();
        }

        /// <summary>The inspector over the report panel, whose previews and builds follow the document.</summary>
        private VisualElement SidePanel()
        {
            _preview = new MapEditorPreview(_document, _canvas, _selection, _previewStrip);
            var side = new VisualElement();
            side.AddToClassList("hrts-map-editor__side");
            side.Add(_inspector);
            side.Add(_preview.Panel);
            return side;
        }

        private void OnDocumentChanged()
        {
            if (_canvas == null)
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
            if (_canvas == null)
            {
                return;
            }

            _list.ShowSelection();
            _inspector.Show(_selection.Current);
            _canvas.Refresh();
        }

        private void OnGhostsToggled(bool on)
        {
            if (_canvas != null)
            {
                _canvas.ShowGhosts = on;
            }
        }

        private void PollDisk() => _document?.ReloadIfChangedOnDisk();
    }
}
