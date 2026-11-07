using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.PlayMode.DebugDraw;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Scene view overlay: the remembered debug-draw layers in groups and, in Play mode, a table of the AI players.</summary>
    [Overlay(typeof(SceneView), "HyperRTS Debug")]
    public class DebugOverlay : Overlay
    {
        private const long PollMilliseconds = 500;

        public override VisualElement CreatePanelContent()
        {
            var root = EditorAssets.Layout(typeof(DebugOverlay)).Instantiate();
            var toggles = BuildLayers(root.Require<VisualElement>("layers"));
            root.Require<Button>("hide-all").clicked += () =>
            {
                DebugLayerHost.HideAll();
                foreach (var (toggle, _) in toggles)
                {
                    toggle.SetValueWithoutNotify(false);
                }

                SceneView.RepaintAll();
            };

            var hint = root.Require<Label>("hint");
            var ai = root.Require<VisualElement>("ai");
            void Poll()
            {
                hint.SetHidden(EditorApplication.isPlaying);
                ai.SetHidden(!EditorApplication.isPlaying);
                if (EditorApplication.isPlaying)
                {
                    ShowAIPlayers(ai);
                }
            }

            Poll();
            root.schedule.Execute(Poll).Every(PollMilliseconds);
            return root;
        }

        private static List<(Toggle, DebugLayer)> BuildLayers(VisualElement host)
        {
            var toggles = new List<(Toggle, DebugLayer)>();
            string group = null;
            foreach (var layer in DebugLayerHost.Layers)
            {
                if (layer.Group != group)
                {
                    group = layer.Group;
                    var heading = new Label(group);
                    heading.AddToClassList("hrts-debug-overlay__group");
                    host.Add(heading);
                }

                var toggle = new Toggle(layer.Label) { value = layer.Shown, tooltip = layer.Detail };
                toggle.RegisterValueChangedCallback(change =>
                {
                    layer.Shown = change.newValue;
                    SceneView.RepaintAll();
                });
                var row = new VisualElement { tooltip = layer.Detail };
                row.AddToClassList("hrts-debug-overlay__row");
                var glyph = new VisualElement();
                glyph.AddToClassList("hrts-glyph");
                glyph.AddToClassList("hrts-glyph--" + layer.Glyph);
                row.Add(glyph);
                row.Add(toggle);
                host.Add(row);
                toggles.Add((toggle, layer));
            }

            return toggles;
        }

        private static void ShowAIPlayers(VisualElement table)
        {
            table.Clear();
            if (!PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return;
            }

            using var query = entityManager.CreateEntityQuery(typeof(Player), typeof(AIPlayer));
            using var players = query.ToEntityArray(Allocator.Temp);
            if (players.Length == 0)
            {
                table.Add(new Label("No AI players."));
                return;
            }

            table.Add(Row("AI", "Think", "Wave", "hrts-debug-overlay__group"));
            foreach (var entity in players)
            {
                var player = entityManager.GetComponentData<Player>(entity);
                var ai = entityManager.GetComponentData<AIPlayer>(entity);
                table.Add(Row(player.Name.ToString(), $"{ai.TimeUntilThink:0.0}s", ai.AttackWaveSize.ToString(), null));
            }
        }

        private static VisualElement Row(string name, string think, string wave, string style)
        {
            var row = new VisualElement();
            row.AddToClassList("hrts-debug-overlay__row");
            var nameCell = new Label(name);
            nameCell.AddToClassList("hrts-debug-overlay__name");
            row.Add(nameCell);
            foreach (var text in new[] { think, wave })
            {
                var cell = new Label(text);
                cell.AddToClassList("hrts-debug-overlay__cell");
                row.Add(cell);
            }

            if (style != null)
            {
                row.AddToClassList(style);
            }

            return row;
        }
    }
}
