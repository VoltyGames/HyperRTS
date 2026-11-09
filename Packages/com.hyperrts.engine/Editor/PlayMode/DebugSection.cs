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
    /// <summary>The debug layers in their groups, each with a status line while on, and in Play mode a table of the AI players.</summary>
    internal class DebugSection
    {
        private const long PollMilliseconds = 250;

        private readonly List<LayerRow> _rows = new();
        private readonly Label _hint;
        private readonly VisualElement _aiGroup;
        private readonly VisualElement _ai;

        /// <summary>Wires a cloned DebugSection.uxml and polls the statuses and AI table while it is shown.</summary>
        public DebugSection(VisualElement root)
        {
            BuildLayers(root.Require<VisualElement>("layers"));
            root.Require<Button>("hide-all").clicked += HideAll;
            _hint = root.Require<Label>("hint");
            _aiGroup = root.Require<VisualElement>("ai-group");
            _ai = root.Require<VisualElement>("ai");

            Poll();
            root.schedule.Execute(Poll).Every(PollMilliseconds);
        }

        private void Poll()
        {
            var playing = EditorApplication.isPlaying;
            _hint.SetHidden(playing);
            _aiGroup.SetHidden(!playing);
            foreach (var row in _rows)
            {
                row.Refresh(playing);
            }

            if (playing)
            {
                ShowAIPlayers(_ai);
            }
        }

        private void HideAll()
        {
            DebugLayerHost.HideAll();
            Poll();
        }

        private void BuildLayers(VisualElement host)
        {
            VisualElement column = null;
            string group = null;
            foreach (var layer in DebugLayerHost.Layers)
            {
                if (layer.Group != group)
                {
                    group = layer.Group;
                    column = new VisualElement();
                    column.AddToClassList("hrts-debug__group");
                    var heading = new Label(group);
                    heading.AddToClassList("hrts-debug__heading");
                    column.Add(heading);
                    host.Add(column);
                }

                var row = new LayerRow(layer);
                column.Add(row.Root);
                _rows.Add(row);
            }
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
                var none = new Label("No AI players.");
                none.AddToClassList("hrts-hint");
                table.Add(none);
                return;
            }

            table.Add(Row("Player", "Next think", "Wave", "hrts-debug__head"));
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

        /// <summary>One layer's switch and, while it is on in Play, its status line.</summary>
        private sealed class LayerRow
        {
            private readonly DebugLayer _layer;
            private readonly Toggle _toggle;
            private readonly Label _status;

            public LayerRow(DebugLayer layer)
            {
                _layer = layer;
                Root = new VisualElement { tooltip = layer.Detail };
                var line = new VisualElement();
                line.AddToClassList("hrts-debug__row");
                line.Add(GlyphElements.CreateGlyph(layer.Glyph));
                _toggle = new Toggle { text = layer.Label, value = layer.Shown };
                _toggle.RegisterValueChangedCallback(change => _layer.Shown = change.newValue);
                line.Add(_toggle);
                Root.Add(line);
                _status = new Label();
                _status.AddToClassList("hrts-debug__status");
                Root.Add(_status);
            }

            public VisualElement Root { get; }

            // The layer may have changed elsewhere, so the switch follows it.
            public void Refresh(bool playing)
            {
                _toggle.SetValueWithoutNotify(_layer.Shown);
                var text = playing && _layer.Shown ? Describe() : null;
                _status.text = text;
                _status.SetHidden(string.IsNullOrEmpty(text));
            }

            private string Describe()
            {
                if (!PlayWorld.TryGet(_layer.Source, out var entityManager))
                {
                    return "No world to draw from.";
                }

                var described = _layer.Describe(entityManager);
                if (described != null)
                {
                    return described;
                }

                return DebugLayerHost.IsEmpty(_layer) ? "Nothing to draw yet." : null;
            }
        }
    }
}
