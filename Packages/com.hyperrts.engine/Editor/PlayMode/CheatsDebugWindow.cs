using HyperRTS.Editor.Common;
using HyperRTS.Editor.Hub;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Cheats &amp; Debug: the cheats and debug-draw layers in a window that docks beside the Game view.</summary>
    public class CheatsDebugWindow : EditorWindow
    {
        private const string WindowTitle = "Cheats & Debug";

        [MenuItem(EditorMenu.CheatsAndDebug, false, EditorMenu.CheatsAndDebugPriority)]
        public static void Open() => GetWindow<CheatsDebugWindow>(WindowTitle);

        public void CreateGUI()
        {
            var root = rootVisualElement;
            EditorAssets.Layout(typeof(CheatsDebugWindow)).CloneTree(root);
            EditorState.Bind(root);
            new CheatsSection(root.Require<VisualElement>("cheats"));
            new DebugSection(root.Require<VisualElement>("debug"));
        }
    }
}
