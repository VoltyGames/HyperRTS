using HyperRTS.Core;
using UnityEditor;

namespace HyperRTS.Editor
{
    /// <summary>HyperRTS ▸ Documentation: opens the guides in the browser.</summary>
    internal static class DocumentationMenu
    {
        [MenuItem(EditorMenu.Documentation + "Getting Started", false, EditorMenu.DocumentationPriority)]
        private static void OpenGettingStarted() => Help.BrowseURL(HyperRTSDocs.GettingStarted);

        [MenuItem(EditorMenu.Documentation + "Module Reference", false, EditorMenu.DocumentationPriority + 1)]
        private static void OpenModules() => Help.BrowseURL(HyperRTSDocs.Modules);

        [MenuItem(EditorMenu.Documentation + "Editor", false, EditorMenu.DocumentationPriority + 2)]
        private static void OpenEditor() => Help.BrowseURL(HyperRTSDocs.Editor);

        [MenuItem(EditorMenu.Documentation + "Networking", false, EditorMenu.DocumentationPriority + 3)]
        private static void OpenNetworking() => Help.BrowseURL(HyperRTSDocs.Networking);
    }
}
