using HyperRTS.Simulation.Common.Authoring;
using UnityEditor;

namespace HyperRTS.Editor.Hub
{
    /// <summary>HyperRTS ▸ Documentation: opens the guides in the browser; each component's help button opens its own section.</summary>
    internal static class DocumentationMenu
    {
        [MenuItem(EditorMenu.Documentation, false, EditorMenu.DocumentationPriority)]
        private static void Open() => Help.BrowseURL(HyperRTSDocs.Index);
    }
}
