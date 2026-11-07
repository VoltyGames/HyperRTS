using HyperRTS.Editor.Common;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>A page of the HyperRTS Tools window. Subclass it (engine or game) and it appears in the sidebar.</summary>
    public abstract class HubPage
    {
        public abstract string Title { get; }

        /// <summary>Sidebar position, lowest first.</summary>
        public abstract int Order { get; }

        /// <summary>The glyph beside the title: a name from Icons/Glyphs.uss, such as "shield-check".</summary>
        public abstract string Glyph { get; }

        /// <summary>The count pill on the sidebar entry, or <see cref="PageBadge.None"/>.</summary>
        public virtual PageBadge Badge => PageBadge.None;

        /// <summary>Builds the page's content each time the page is shown; it leaves the panel when the page hides.</summary>
        public abstract VisualElement Build();

        /// <summary>The page's <c>&lt;Page&gt;&lt;part&gt;.uxml</c>, such as MapsPageRow.uxml, from its own package or Assets.</summary>
        protected VisualTreeAsset Layout(string part = "") => EditorAssets.Layout(GetType(), part);
    }
}
