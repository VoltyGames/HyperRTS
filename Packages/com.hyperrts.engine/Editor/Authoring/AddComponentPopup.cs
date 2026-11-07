using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>A searchable list of the authoring components the object lacks, grouped by their Add Component folder.</summary>
    public sealed class AddComponentPopup : PopupWindowContent
    {
        private const string NoFolder = "Other";

        private readonly GameObject _target;
        private readonly List<(VisualElement Group, List<Button> Items)> _groups = new();

        public AddComponentPopup(GameObject target)
        {
            _target = target;
        }

        public override Vector2 GetWindowSize() => new(260f, 340f);

        public override VisualElement CreateGUI()
        {
            var root = EditorAssets.Layout(typeof(AddComponentPopup)).Instantiate();
            var list = root.Require<ScrollView>("list");
            var search = root.Require<ToolbarSearchField>("search");
            foreach (var folder in Missing().GroupBy(entry => entry.Folder).OrderBy(group => group.Key))
            {
                list.Add(CreateGroup(folder.Key, folder.OrderBy(entry => entry.Name)));
            }

            search.RegisterValueChangedCallback(change => Filter(change.newValue));
            root.schedule.Execute(search.Focus);
            return root;
        }

        /// <summary>The Add Component folder and name of a component type; types outside the menu go in "Other".</summary>
        public static (string Folder, string Name) MenuEntry(Type type)
        {
            var menu = type.GetCustomAttribute<AddComponentMenu>()?.componentMenu;
            if (string.IsNullOrEmpty(menu))
            {
                return (NoFolder, ObjectNames.NicifyVariableName(type.Name.Replace("Authoring", "")));
            }

            var slash = menu.LastIndexOf('/');
            return slash < 0 ? (NoFolder, menu) : (menu.Substring(0, slash), menu.Substring(slash + 1));
        }

        private IEnumerable<(string Folder, string Name, Type Type)> Missing()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<AuthoringBehaviour>())
            {
                if (type.IsAbstract || type.IsGenericType || _target.GetComponent(type) != null)
                {
                    continue;
                }

                var (folder, name) = MenuEntry(type);
                yield return (folder, name, type);
            }
        }

        private VisualElement CreateGroup(string folder, IEnumerable<(string Folder, string Name, Type Type)> entries)
        {
            var group = new VisualElement();
            var title = new Label(folder);
            title.AddToClassList("hrts-popup__group");
            group.Add(title);

            var items = new List<Button>();
            foreach (var (_, name, type) in entries)
            {
                var item = new Button(() => Add(type)) { text = name };
                item.AddToClassList("hrts-popup__item");
                group.Add(item);
                items.Add(item);
            }

            _groups.Add((group, items));
            return group;
        }

        private void Filter(string text)
        {
            foreach (var (group, items) in _groups)
            {
                var anyShown = false;
                foreach (var item in items)
                {
                    var matches = item.text.Contains(text, StringComparison.OrdinalIgnoreCase);
                    item.SetHidden(!matches);
                    anyShown |= matches;
                }

                group.SetHidden(!anyShown);
            }
        }

        private void Add(Type type)
        {
            Undo.AddComponent(_target, type);
            editorWindow.Close();
        }
    }
}
