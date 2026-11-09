using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Scene view overlay: the remembered debug-draw layers in groups and, in Play mode, a table of the AI players.</summary>
    [Overlay(typeof(SceneView), "HyperRTS Debug")]
    public class DebugOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = EditorAssets.Layout(typeof(DebugOverlay)).Instantiate();
            new DebugSection(root.Require<VisualElement>("debug"));
            return root;
        }
    }
}
