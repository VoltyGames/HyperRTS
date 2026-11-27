using System;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD;

/// <summary>Shorthands for UXML templates, labels, localized text and visibility, shared by the HUD and the editor tools.</summary>
public static class HUDElements
{
    public static Label Text(string text, string className, VisualElement parent = null)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        parent?.Add(label);
        return label;
    }

    extension(VisualTreeAsset template)
    {
        /// <summary>A fresh copy of a template's one root element, detached from the template's wrapper.</summary>
        public VisualElement CloneRoot()
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
    }

    extension(VisualElement tree)
    {
        /// <summary>Replaces each template instance in the tree by its one root, named after the instance.</summary>
        public void UnwrapInstances()
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
    }

    extension(VisualElement root)
    {
        /// <summary>The element called <paramref name="name"/>, which the layout must have.</summary>
        public T Require<T>(string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException(
                $"HyperRTS: the layout has no {typeof(T).Name} named '{name}'.");
    }

    extension(VisualElement element)
    {
        /// <summary>Removes the element from layout through the <c>hrts-hidden</c> class, which HUD.uss and HyperRTSEditor.uss define.</summary>
        public void SetHidden(bool hidden) =>
            element.EnableInClassList("hrts-hidden", hidden);

        /// <summary>Hides the element but keeps its space, so neighbouring elements don't shift.</summary>
        public void SetInvisible(bool invisible) =>
            element.EnableInClassList("hrts-invisible", invisible);

        /// <summary>Runs <paramref name="on"/> each time the element joins a panel and <paramref name="off"/> each time it leaves.</summary>
        public void WhileAttached(Action on, Action off)
        {
            element.RegisterCallback<AttachToPanelEvent>(_ => on());
            element.RegisterCallback<DetachFromPanelEvent>(_ => off());
            if (element.panel != null)
            {
                on();
            }
        }

        /// <summary>Removes the element from layout, for elements outside the HUD's sheets.</summary>
        public void SetVisible(bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    extension(TextElement element)
    {
        /// <summary>Binds the element's text to a string table entry, filling its placeholders from the arguments.</summary>
        public void Localize(string table, string entry, params object[] arguments) =>
            element.SetBinding(nameof(TextElement.text), new LocalizedText(table, entry, arguments));
    }
}
