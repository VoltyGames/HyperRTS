using System.IO;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Maps.Generation;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>
    /// HyperRTS ▸ Map Editor: edits a map spec's authored sector on a top-down canvas, with the feature list on the
    /// left and the selected feature's fields on the right. Double-clicking a <c>.map.json</c> opens it here.
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string WindowTitle = "Map Editor";
        private const long DiskPollMilliseconds = 1000;

        [SerializeField] private MapSpecDocument _document;
        [SerializeField] private string _specPath;

        private readonly MapFeatureSelection _selection = new();
        private readonly MapToolbox _toolbox = new();
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
            _toolbar = new MapEditorToolbar(path => Load(path), Save, Revert, OnGhostsToggled);
            root.Add(_toolbar);
            _toolbar.Insert(1, new MapToolStrip(_toolbox));
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

        public override void SaveChanges()
        {
            _document.Save();
            base.SaveChanges();
        }

        private void OnEnable()
        {
            saveChangesMessage = "This map spec has unsaved changes. Save them?";
            _selection.Changed += OnSelectionChanged;
        }

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

        private void OnDestroy()
        {
            if (_document != null)
            {
                DestroyImmediate(_document);
            }
        }

        /// <summary>Opens a spec, first asking what to do with unsaved edits; false when the user cancels.</summary>
        private bool Load(string specPath)
        {
            Focus();
            if (_document != null && _document.Path == specPath)
            {
                return true;
            }

            if (!ConfirmDiscard())
            {
                _toolbar?.Show(_document);
                return false;
            }

            if (_document == null)
            {
                _document = MapSpecDocument.Open(specPath);
            }
            else
            {
                _document.Load(specPath);
            }

            _specPath = specPath;
            _selection.Clear();
            BuildBody();
            return true;
        }

        private bool ConfirmDiscard()
        {
            if (_document == null || !_document.IsDirty)
            {
                return true;
            }

            var choice = EditorUtility.DisplayDialogComplex(WindowTitle, $"Save changes to {_document.MapName}?",
                "Save", "Cancel", "Discard");
            if (choice == 0)
            {
                _document.Save();
            }

            return choice != 1;
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
                _toolbar.ShowNone();
                var hint = new Label("Pick a map spec in the toolbar, or double-click a .map.json file.");
                hint.AddToClassList("hrts-hint");
                _body.Add(hint);
                return;
            }

            _list = new MapFeatureList(_selection);
            _canvas = new MapCanvas(_document, _selection, _toolbox) { ShowGhosts = _toolbar.Ghosts };
            _inspector = new MapFeatureInspector(_document);
            _body.Add(_list);
            _body.Add(_canvas);
            _body.Add(SidePanel());
            _body.TrackSerializedObjectValue(_document.Serialized, _ => _document.NotifyChanged());

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

            _selection.Validate(_document.spec);
            _list.Refresh(_document.spec);
            _canvas.Refresh();
            _toolbar.Show(_document);
            hasUnsavedChanges = _document.IsDirty;
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

        private void Save() => _document.Save();

        private void Revert() => _document.Revert();

        private void PollDisk()
        {
            if (_document != null)
            {
                _document.ReloadIfChangedOnDisk();
            }
        }
    }
}
