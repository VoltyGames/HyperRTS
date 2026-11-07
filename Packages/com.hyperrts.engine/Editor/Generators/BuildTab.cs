using HyperRTS.Editor.Common;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Generators
{
    /// <summary>A tab of the Build page. Subclass it (engine or game) and it appears there.</summary>
    public abstract class BuildTab
    {
        public abstract string Title { get; }

        /// <summary>Tab position, lowest first.</summary>
        public abstract int Order { get; }

        /// <summary>Builds the tab's content each time the tab is shown; it leaves the panel when the tab hides.</summary>
        public abstract VisualElement Build();

        /// <summary>The tab's <c>&lt;Tab&gt;&lt;part&gt;.uxml</c>, such as GeneratorsTabRow.uxml, from its own package or Assets.</summary>
        protected VisualTreeAsset Layout(string part = "") => EditorAssets.Layout(GetType(), part);
    }
}
