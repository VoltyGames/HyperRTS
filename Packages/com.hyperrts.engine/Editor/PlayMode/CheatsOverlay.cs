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
        private const long PollMilliseconds = 250;

        public override VisualElement CreatePanelContent()
        {
            var root = EditorAssets.Layout(typeof(CheatsOverlay)).Instantiate();
            EditorState.Bind(root);
            var hint = root.Require<Label>("hint");
            var content = root.Require<VisualElement>("cheats");
            var cheats = new CheatsSection(content);
            void Poll()
            {
                var playing = EditorApplication.isPlaying;
                hint.SetHidden(playing);
                content.SetHidden(!playing);
                if (playing)
                {
                    cheats.Poll();
                }
            }

            Poll();
            root.schedule.Execute(Poll).Every(PollMilliseconds);
            return root;
        }
    }
}
