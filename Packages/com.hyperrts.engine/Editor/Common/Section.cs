using UnityEngine.UIElements;

namespace HyperRTS.Editor.Common
{
    /// <summary>A titled block of editor controls; its children follow the title.</summary>
    [UxmlElement]
    public sealed partial class Section : VisualElement
    {
        private readonly Label _title = new();

        public Section()
        {
            AddToClassList("hrts-section");
            _title.AddToClassList("hrts-section__title");
            hierarchy.Add(_title);
        }

        [UxmlAttribute]
        public string Title
        {
            get => _title.text;
            set => _title.text = value;
        }
    }
}
