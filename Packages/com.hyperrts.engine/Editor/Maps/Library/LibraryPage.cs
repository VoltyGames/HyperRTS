using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Tools ▸ Library: browse the map library by type, edit an item's file, create, duplicate and rename items, see
    /// what names an item, and the library's load problems.
    /// </summary>
    public sealed class LibraryPage : HubPage
    {
        internal const string Root = "Assets";
        private const long ChangePollMilliseconds = 700;

        private readonly List<Type> _typeList = MapLibrary.ItemTypes.ToList();
        private MapLibrary _library;
        private List<MapLibraryProblem> _problems;
        private int _problemsVersion = -1;
        private int _version = -1;
        private Type _type = MapLibrary.ItemTypes[0];
        private ListView _types;
        private LibraryItemList _items;
        private LibraryItemForm _form;
        private LibraryUsedBy _usedBy;
        private LibraryProblemsView _problemView;

        public override string Title => "Library";

        public override int Order => 15;

        /// <summary>The number of library problems, rescanned after the project changes.</summary>
        public override string Badge
        {
            get
            {
                ScanProblems();
                return _problems.Count > 0 ? _problems.Count.ToString() : null;
            }
        }

        public override VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("hrts-library");
            root.Add(Toolbar());
            var body = new VisualElement();
            body.AddToClassList("hrts-library__body");
            _types = new ListView(_typeList, 20f, () => new Label(), BindType) { selectionType = SelectionType.Single };
            _types.AddToClassList("hrts-library-types");
            _types.selectionChanged += picked => SelectType(picked.OfType<Type>().FirstOrDefault());
            body.Add(_types);
            _items = new LibraryItemList();
            _items.Selected += SelectItem;
            body.Add(_items);
            var detail = new ScrollView();
            detail.AddToClassList("hrts-library-detail");
            _form = new LibraryItemForm();
            detail.Add(_form);
            _usedBy = new LibraryUsedBy(Follow);
            detail.Add(_usedBy);
            body.Add(detail);
            root.Add(body);
            _problemView = new LibraryProblemsView(Reload);
            root.Add(_problemView);
            root.schedule.Execute(ReloadIfChanged).Every(ChangePollMilliseconds);
            Reload();
            _types.SetSelectionWithoutNotify(new[] { _typeList.IndexOf(_type) });
            return root;
        }

        public override void Release()
        {
            if (_form == null)
            {
                return;
            }

            AskToSave();
            _form.Close();
            (_types, _items, _form, _usedBy, _problemView) = (null, null, null, null, null);
        }

        /// <summary>Reloads after a file action and opens the item it made.</summary>
        internal void Created(Type type, string key)
        {
            Reload();
            SelectType(type);
            SelectItem(key);
            _items.Highlight(key);
        }

        private Toolbar Toolbar()
        {
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => LibraryActions.New(this, _library, _type, _form.Folder)) { text = "New" });
            toolbar.Add(new ToolbarButton(() => LibraryActions.Duplicate(this, _library, _form)) { text = "Duplicate" });
            toolbar.Add(new ToolbarButton(Rename) { text = "Rename" });
            toolbar.Add(new ToolbarButton(Reveal) { text = "Reveal in Project" });
            toolbar.Add(new ToolbarSpacer { flex = true });
            toolbar.Add(new ToolbarButton(Reload) { text = "Reload" });
            return toolbar;
        }

        private void ReloadIfChanged()
        {
            if (_version != MapLibraryChanges.Version)
            {
                Reload();
            }
        }

        private void Reload()
        {
            _version = MapLibraryChanges.Version;
            _problemsVersion = -1;
            ScanProblems();
            _library = LoadLibrary();
            _types.RefreshItems();
            _items.Show(_library, _type);
            ReopenForm();
            _problemView.Show(_problems);
        }

        /// <summary>The library, or null when its files don't index (the problems list says why).</summary>
        private static MapLibrary LoadLibrary()
        {
            try
            {
                return MapLibrary.Load(Root);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Re-reads the open item after an outside change, unless it has unsaved edits.</summary>
        private void ReopenForm()
        {
            if (_form.File == null || _form.IsDirty)
            {
                ShowUsages();
                return;
            }

            var type = _form.Type;
            var key = _form.Key;
            _form.Close();
            if (_library != null && _library.Contains(type, key))
            {
                Open(type, key);
            }

            ShowUsages();
        }

        private void BindType(VisualElement element, int index)
        {
            var type = _typeList[index];
            var count = _library == null ? "?" : _library.Keys(type).Count.ToString();
            ((Label)element).text = $"{MapLibrary.TypeName(type)} ({count})";
        }

        private void SelectType(Type type)
        {
            if (type == null || type == _type)
            {
                return;
            }

            _type = type;
            _types.SetSelectionWithoutNotify(new[] { _typeList.IndexOf(type) });
            _items.Show(_library, type);
            _items.Highlight(_form.Type == type ? _form.Key : null);
        }

        private void SelectItem(string key)
        {
            if (_form.Type == _type && _form.Key == key)
            {
                return;
            }

            if (!AskToSave())
            {
                _items.Highlight(_form.Type == _type ? _form.Key : null);
                return;
            }

            Open(_type, key);
            ShowUsages();
        }

        private void Open(Type type, string key)
        {
            try
            {
                _form.Open(type, key, _library.FileOf(type, key));
            }
            catch (InvalidOperationException e)
            {
                _form.Close();
                Debug.LogError(e.Message);
            }
        }

        /// <summary>Offers to save unsaved edits; false when the user cancels.</summary>
        private bool AskToSave()
        {
            if (!_form.IsDirty)
            {
                return true;
            }

            var choice = EditorUtility.DisplayDialogComplex("Unsaved changes", $"Save the changes to {_form.File}?",
                "Save", "Cancel", "Discard");
            if (choice == 0)
            {
                _form.Save();
            }

            return choice != 1;
        }

        private void ShowUsages()
        {
            if (_form.File == null)
            {
                _usedBy.Hide();
                return;
            }

            _usedBy.Unhide();
            try
            {
                _usedBy.Show(MapLibraryUsages.Find(_library, _form.Type, _form.Key));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                _usedBy.Fail(e.Message);
            }
        }

        private void Follow(MapLibraryUsage usage)
        {
            if (usage.Type == null)
            {
                EditorAssets.Reveal(AssetDatabase.LoadAssetAtPath<Object>(usage.File));
                return;
            }

            SelectType(usage.Type);
            SelectItem(usage.Key);
            _items.Highlight(_form.Key);
        }

        private void ScanProblems()
        {
            if (_problemsVersion == MapLibraryChanges.Version)
            {
                return;
            }

            _problems = MapLibraryProblems.Find(Root);
            _problemsVersion = MapLibraryChanges.Version;
        }

        private void Rename()
        {
            if (AskToSave())
            {
                LibraryActions.Rename(this, _library, _form);
            }
        }

        private void Reveal()
        {
            if (_form.File != null)
            {
                EditorAssets.Reveal(AssetDatabase.LoadAssetAtPath<Object>(_form.File));
            }
        }
    }
}
