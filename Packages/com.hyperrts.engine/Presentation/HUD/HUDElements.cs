using System;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Shorthands for UXML templates, labels and visibility, shared by the HUD and the editor tools.</summary>
    public static class HUDElements
    {
        public static Label Text(string text, string className, VisualElement parent = null)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent?.Add(label);
            return label;
        }

        /// <summary>A fresh copy of a template's one root element, detached from the template's wrapper.</summary>
        public static VisualElement CloneRoot(this VisualTreeAsset template)
        {
            var tree = template.Instantiate();
            if (tree.childCount != 1)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: layout '{template.name}' needs one top element, not {tree.childCount}.");
            }

            var root = tree[0];
            root.RemoveFromHierarchy();
            return root;
        }

        /// <summary>Replaces each template instance in the tree by its one root, named after the instance.</summary>
        public static void UnwrapInstances(this VisualElement tree)
        {
            foreach (var slot in tree.Query<TemplateContainer>().ToList())
            {
                var shell = slot[0];
                shell.name = slot.name;
                shell.RemoveFromHierarchy();
                slot.parent.Insert(slot.parent.IndexOf(slot), shell);
                slot.RemoveFromHierarchy();
            }
        }

        /// <summary>The element called <paramref name="name"/>, which the layout must have.</summary>
        public static T Require<T>(this VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException(
                $"HyperRTS: the layout has no {typeof(T).Name} named '{name}'.");

        /// <summary>Removes the element from layout through the <c>hrts-hidden</c> class, which HUD.uss and HyperRTSEditor.uss define.</summary>
        public static void SetHidden(this VisualElement element, bool hidden) =>
            element.EnableInClassList("hrts-hidden", hidden);

        /// <summary>Hides the element but keeps its space, so neighbouring elements don't shift.</summary>
        public static void SetInvisible(this VisualElement element, bool invisible) =>
            element.EnableInClassList("hrts-invisible", invisible);

        /// <summary>Runs <paramref name="on"/> each time the element joins a panel and <paramref name="off"/> each time it leaves.</summary>
        public static void WhileAttached(this VisualElement element, Action on, Action off)
        {
            element.RegisterCallback<AttachToPanelEvent>(_ => on());
            element.RegisterCallback<DetachFromPanelEvent>(_ => off());
            if (element.panel != null)
            {
                on();
            }
        }

        /// <summary>Removes the element from layout, for elements outside the HUD's sheets.</summary>
        public static void SetVisible(this VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
