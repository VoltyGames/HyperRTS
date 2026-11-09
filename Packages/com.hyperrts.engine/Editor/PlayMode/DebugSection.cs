using System.Collections.Generic;
using HyperRTS.Editor.Icons;
using HyperRTS.Editor.PlayMode.DebugDraw;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.AI;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using UnityEditor;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>The remembered debug-draw layers in groups and, in Play mode, a table of the AI players.</summary>
    internal class DebugSection
    {
        private const long PollMilliseconds = 500;

        private readonly List<Toggle> _toggles;
        private readonly Label _hint;
        private readonly VisualElement _ai;

        /// <summary>Wires a cloned DebugSection.uxml and polls the AI table while it is shown.</summary>
        public DebugSection(VisualElement root)
        {
            _toggles = BuildLayers(root.Require<VisualElement>("layers"));
            root.Require<Button>("hide-all").clicked += HideAll;
            _hint = root.Require<Label>("hint");
            _ai = root.Require<VisualElement>("ai");

            Poll();
            root.schedule.Execute(Poll).Every(PollMilliseconds);
        }

        // Another section may have changed a layer since the last poll, so the toggles follow the layers.
        private void Poll()
        {
            var playing = EditorApplication.isPlaying;
            _hint.SetHidden(playing);
            _ai.SetHidden(!playing);
            var index = 0;
            foreach (var layer in DebugLayerHost.Layers)
            {
                _toggles[index++].SetValueWithoutNotify(layer.Shown);
            }

            if (playing)
            {
                ShowAIPlayers(_ai);
            }
        }

        private void HideAll()
        {
            DebugLayerHost.HideAll();
            foreach (var toggle in _toggles)
            {
                toggle.SetValueWithoutNotify(false);
            }

            SceneView.RepaintAll();
        }

        private static List<Toggle> BuildLayers(VisualElement host)
        {
            var toggles = new List<Toggle>();
            string group = null;
            foreach (var layer in DebugLayerHost.Layers)
            {
                if (layer.Group != group)
                {
                    group = layer.Group;
                    var heading = new Label(group);
                    heading.AddToClassList("hrts-debug__group");
                    host.Add(heading);
                }

                var toggle = new Toggle(layer.Label) { value = layer.Shown, tooltip = layer.Detail };
                toggle.RegisterValueChangedCallback(change =>
                {
                    layer.Shown = change.newValue;
                    SceneView.RepaintAll();
                });
                var row = new VisualElement { tooltip = layer.Detail };
                row.AddToClassList("hrts-debug__row");
                row.Add(GlyphElements.CreateGlyph(layer.Glyph));
                row.Add(toggle);
                host.Add(row);
                toggles.Add(toggle);
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

            table.Add(Row("AI", "Think", "Wave", "hrts-debug__group"));
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
            row.AddToClassList("hrts-debug__row");
            var nameCell = new Label(name);
            nameCell.AddToClassList("hrts-debug__name");
            row.Add(nameCell);
            foreach (var text in new[] { think, wave })
            {
                var cell = new Label(text);
                cell.AddToClassList("hrts-debug__cell");
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
