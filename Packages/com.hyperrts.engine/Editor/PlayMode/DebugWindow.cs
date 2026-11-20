using HyperRTS.Editor.Hub;
using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Debug: the debug layers and, in Play, a table of the AI players.</summary>
    public class DebugWindow : PlayModeWindow
    {
        [MenuItem(EditorMenu.Debug, false, EditorMenu.DebugPriority)]
        public static void Open() => Open<DebugWindow>("Debug");

        protected override void Build(VisualElement root) => new DebugPanel(root.Require<VisualElement>("debug"));
    }
}
