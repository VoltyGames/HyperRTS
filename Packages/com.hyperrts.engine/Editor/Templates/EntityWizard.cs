using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// The New Entity dialog: saves a picked base prefab or engine template as a named prefab, then runs the game's
    /// <see cref="EntityWizardStep"/>s on it.
    /// </summary>
    public sealed class EntityWizard : EditorWindow
    {
        private const string DialogTitle = "New Entity";

        [SerializeField] private string _folder;
        private TreeView _choices;
        private TextField _name;
        private Button _create;
        private Label _problem;
        private List<EntityWizardStep> _steps;
        private List<(EntityChoice Heading, List<EntityChoice> Choices)> _all;

        [MenuItem("Assets/Create/HyperRTS/Entity...", false, 0)]
        private static void Open()
        {
            var window = CreateInstance<EntityWizard>();
            window.titleContent = new GUIContent(DialogTitle);
            window._folder = SelectedFolder();
            window.minSize = new Vector2(380f, 420f);
            window.ShowUtility();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(EntityWizard)).CloneTree(root);
            var row = EditorAssets.Layout(typeof(EntityWizard), "Row");
            _choices = root.Require<TreeView>("choices");
            _choices.makeItem = row.CloneRoot;
            _choices.bindItem = BindRow;
            _choices.selectionChanged += _ => Refresh();
            _choices.itemsChosen += _ => _name.Focus();
            _name = root.Require<TextField>("name");
            _name.RegisterValueChangedCallback(_ => Refresh());
            _create = root.Require<Button>("create");
            _create.clicked += Create;
            root.Require<ToolbarSearchField>("search").RegisterValueChangedCallback(change => Fill(change.newValue));
            root.Require<Label>("folder").text = $"Saves in {_folder}";
            _problem = root.Require<Label>("problem");
            var form = root.Require<VisualElement>("steps");
            _steps = EntityWizardStep.For(_folder);
            _steps.ForEach(step => form.Add(step.CreateForm(Refresh)));
            _all = EntityChoice.All().ToList();
            Fill("");
        }

        private void Fill(string search)
        {
            var items = new List<TreeViewItemData<EntityChoice>>();
            var id = 0;
            foreach (var (heading, choices) in _all)
            {
                var shown = choices
                    .Where(choice => choice.Label.Contains(search, System.StringComparison.OrdinalIgnoreCase))
                    .Select(choice => new TreeViewItemData<EntityChoice>(++id, choice))
                    .ToList();
                if (shown.Count > 0)
                {
                    items.Add(new TreeViewItemData<EntityChoice>(++id, heading, shown));
                }
            }

            _choices.SetRootItems(items);
            _choices.Rebuild();
            _choices.ExpandAll();
            Refresh();
        }

        private void BindRow(VisualElement row, int index)
        {
            var choice = _choices.GetItemDataForIndex<EntityChoice>(index);
            row.Require<Label>("label").text = choice.Label;
            row.EnableInClassList("hrts-entity-wizard__heading", choice.IsHeading);
            var icon = row.Require<Image>("icon");
            icon.image = choice.Icon;
            icon.SetHidden(choice.Icon == null);
        }

        private EntityChoice Picked => _choices.selectedItem as EntityChoice;

        private void Refresh()
        {
            var picked = Picked;
            var name = _name.value.Trim();
            var ready = picked is { IsHeading: false } && name.Length > 0;
            var problem = ready ? _steps.Select(step => step.Problem(name)).FirstOrDefault(text => text != null) : null;
            _problem.text = problem;
            _problem.SetHidden(problem == null);
            _create.SetEnabled(ready && problem == null);
        }

        private void Create()
        {
            var prefab = Picked.Save(_name.value.Trim(), _folder, _steps);
            ProjectWindowUtil.ShowCreatedAsset(prefab);
            Close();
        }

        private static string SelectedFolder()
        {
            var path = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
        }
    }
}
