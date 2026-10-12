using UnityEngine.UIElements;

namespace HyperRTS.Editor.Hub
{
    /// <summary>A page of the HyperRTS Tools window. Subclass it (engine or game) and it appears in the sidebar.</summary>
    public abstract class HubPage
    {
        public abstract string Title { get; }

        /// <summary>Sidebar position, lowest first.</summary>
        public abstract int Order { get; }

        /// <summary>Short text on the sidebar entry (an error count), or null for none.</summary>
        public virtual string Badge => null;

        /// <summary>Builds the page's content each time the page is shown.</summary>
        public abstract VisualElement Build();

        /// <summary>Drops callbacks and polling when the page is hidden or the window closes.</summary>
        public virtual void Release()
        {
        }
    }
}
