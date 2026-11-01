using System.Collections.Generic;
using HyperRTS.Presentation.HUD;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Common
{
    /// <summary>Named figures in two columns, such as a report's land shares and counts.</summary>
    [UxmlElement]
    public sealed partial class ValueList : VisualElement
    {
        private readonly VisualTreeAsset _row = EditorAssets.Layout(typeof(ValueList), "Row");

        public void Set(IEnumerable<(string Name, string Value)> figures)
        {
            Clear();
            foreach (var (name, value) in figures)
            {
                var row = _row.CloneRoot();
                row.Require<Label>("name").text = name;
                row.Require<Label>("value").text = value;
                Add(row);
            }
        }
    }
}
