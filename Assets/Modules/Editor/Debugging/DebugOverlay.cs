using System;
using System.Text;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>Scene view overlay: debug-draw toggles, live AI state and a shortcut to the cheats window.</summary>
    [Overlay(typeof(SceneView), "HyperRTS Debug")]
    public class DebugOverlay : Overlay
    {
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement { style = { minWidth = 180 } };
            root.Add(Toggle("Nav grid (blocked)", DebugDraw.NavGrid, value => DebugDraw.NavGrid = value));
            root.Add(Toggle("Fog (visible to local)", DebugDraw.Fog, value => DebugDraw.Fog = value));
            root.Add(Toggle("Paths and targets", DebugDraw.Paths, value => DebugDraw.Paths = value));
            root.Add(Toggle("Spatial cells", DebugDraw.Spatial, value => DebugDraw.Spatial = value));

            var ai = new Label { style = { marginTop = 4, whiteSpace = WhiteSpace.Normal } };
            root.Add(ai);
            root.schedule.Execute(() => ai.text = AIStatus()).Every(500);

            root.Add(new Button(CheatsWindow.Open) { text = "Cheats..." });
            return root;
        }

        private static Toggle Toggle(string label, bool value, Action<bool> set)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(change =>
            {
                set(change.newValue);
                SceneView.RepaintAll();
            });
            return toggle;
        }

        private static string AIStatus()
        {
            if (!PlayWorld.TryGet(out var entityManager))
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
