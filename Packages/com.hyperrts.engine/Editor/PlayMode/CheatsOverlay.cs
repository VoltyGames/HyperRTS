using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Scene view overlay with the Play-mode cheats; outside Play it holds a one-line hint.</summary>
    [Overlay(typeof(SceneView), "HyperRTS Cheats")]
    public class CheatsOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = EditorAssets.Layout(typeof(CheatsOverlay)).Instantiate();
            EditorState.Bind(root);
            new CheatsSection(root.Require<VisualElement>("cheats"));
            return root;
        }
    }
}
