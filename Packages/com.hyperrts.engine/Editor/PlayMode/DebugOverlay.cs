using System.Text;
using HyperRTS.Editor.PlayMode.DebugDraw;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Scene view overlay: debug-draw toggles, live AI state and a shortcut to the cheats window.</summary>
    [Overlay(typeof(SceneView), "HyperRTS Debug")]
    public class DebugOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { style = { minWidth = 180 } };
            foreach (var layer in DebugLayerHost.Layers)
            {
                root.Add(Toggle(layer));
            }

            var ai = new Label { style = { marginTop = 4, whiteSpace = WhiteSpace.Normal } };
            root.Add(ai);
            root.schedule.Execute(() => ai.text = AIStatus()).Every(500);

            root.Add(new Button(CheatsWindow.Open) { text = "Cheats..." });
            return root;
        }

        private static Toggle Toggle(DebugLayer layer)
        {
            var toggle = new Toggle(layer.Label) { value = layer.Enabled };
            toggle.RegisterValueChangedCallback(change =>
            {
                layer.Enabled = change.newValue;
                SceneView.RepaintAll();
            });
            return toggle;
        }

        private static string AIStatus()
        {
            if (!PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return "Enter Play mode for live data.";
            }

            using var query = entityManager.CreateEntityQuery(typeof(Player), typeof(AIPlayer));
            using var players = query.ToEntityArray(Allocator.Temp);
            var text = new StringBuilder(players.Length == 0 ? "No AI players." : "AI:");
            foreach (var entity in players)
            {
                var player = entityManager.GetComponentData<Player>(entity);
                var ai = entityManager.GetComponentData<AIPlayer>(entity);
                text.Append($"\n{player.Name}: think in {ai.TimeUntilThink:0.0}s, wave {ai.AttackWaveSize}");
            }

            return text.ToString();
        }
    }
}
