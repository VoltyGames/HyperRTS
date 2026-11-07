using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Icons;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>The Effects page's tree of effect rigs under their kind headings; picking a rig row raises Picked.</summary>
    internal sealed class EffectRigList
    {
        private readonly TreeView _tree;

        // The tree id of each listed rig, by path.
        private readonly Dictionary<string, int> _ids = new();

        public EffectRigList(TreeView tree, VisualTreeAsset row)
        {
            _tree = tree;
            _tree.makeItem = row.CloneRoot;
            _tree.bindItem = BindRow;
            _tree.selectionChanged += _ => Pick();
        }

        public event Action<EffectRigEntry> Picked;

        /// <summary>Lists <paramref name="rigs"/> by kind, in their order, and selects <paramref name="selected"/> if listed.</summary>
        public void Show(IEnumerable<EffectRigEntry> rigs, EffectRigEntry selected)
        {
            _ids.Clear();
            var id = 0;
            var roots = new List<TreeViewItemData<(string, EffectRigEntry)>>();
            foreach (var kind in rigs.GroupBy(rig => rig.Kind))
            {
                var kindId = id++;
                var children = new List<TreeViewItemData<(string, EffectRigEntry)>>();
                foreach (var rig in kind)
                {
                    _ids[rig.Path] = id;
                    children.Add(new TreeViewItemData<(string, EffectRigEntry)>(id++, (rig.Name, rig)));
                }

                roots.Add(new TreeViewItemData<(string, EffectRigEntry)>(kindId, (kind.Key, null), children));
            }

            _tree.SetRootItems(roots);
            _tree.Rebuild();
            _tree.ExpandAll();
            Select(selected);
        }

        /// <summary>Selects and scrolls to <paramref name="rig"/>'s row without raising Picked; clears the selection when it isn't listed.</summary>
        public void Select(EffectRigEntry rig)
        {
            if (rig == null || !_ids.TryGetValue(rig.Path, out var id))
            {
                _tree.ClearSelection();
                return;
            }

            _tree.SetSelectionByIdWithoutNotify(new[] { id });
            _tree.ScrollToItemById(id);
        }

        // A kind heading row has no rig.
        private void BindRow(VisualElement row, int index)
        {
            var (label, rig) = _tree.GetItemDataForIndex<(string, EffectRigEntry)>(index);
            row.Require<Label>("name").text = label;
            row.Require<VisualElement>("glyph").SetGlyph(rig != null ? "sparkles" : "folder-open");
        }

        private void Pick()
        {
            if (_tree.selectedIndex < 0)
            {
                return;
            }

            var (_, rig) = _tree.GetItemDataForIndex<(string, EffectRigEntry)>(_tree.selectedIndex);
            if (rig != null)
            {
                Picked?.Invoke(rig);
            }
        }
    }
}
