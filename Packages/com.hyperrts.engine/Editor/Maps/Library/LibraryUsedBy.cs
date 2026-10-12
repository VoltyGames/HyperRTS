using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>The Library page's "Used by" list: one button per item or map spec naming the open item.</summary>
    public sealed class LibraryUsedBy : VisualElement
    {
        private readonly Label _title = new("Used by");
        private readonly VisualElement _rows = new();
        private readonly Action<MapLibraryUsage> _picked;

        public LibraryUsedBy(Action<MapLibraryUsage> picked)
        {
            _picked = picked;
            AddToClassList("hrts-library-used");
            _title.AddToClassList("hrts-section__title");
            Add(_title);
            Add(_rows);
        }

        public void Show(IReadOnlyList<MapLibraryUsage> usages)
        {
            _rows.Clear();
            _title.text = $"Used by ({usages.Count})";
            if (usages.Count == 0)
            {
                _rows.Add(Note("Nothing names this item."));
                return;
            }

            foreach (var usage in usages)
            {
                var button = new Button(() => _picked(usage)) { text = usage.ToString(), tooltip = usage.File };
                button.AddToClassList("hrts-library-used__item");
                _rows.Add(button);
            }
        }

        /// <summary>Shows why the usages could not be found (a file that does not parse).</summary>
        public void Fail(string message)
        {
            _rows.Clear();
            _title.text = "Used by";
            _rows.Add(new HelpBox(message, HelpBoxMessageType.Error));
        }

        public void Hide() => style.display = DisplayStyle.None;

        public void Unhide() => style.display = DisplayStyle.Flex;

        private static Label Note(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hrts-hint");
            return label;
        }
    }
}
