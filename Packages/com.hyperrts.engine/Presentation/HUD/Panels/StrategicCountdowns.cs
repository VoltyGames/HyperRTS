using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Strikes;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD.Panels
{
    /// <summary>The map-wide strategic asset countdowns: one row per player holding one, in the owner's colour.</summary>
    public sealed class StrategicCountdowns : HUDPanel
    {
        private readonly List<Row> _rows = new();
        private readonly VisualTreeAsset _rowTemplate;

        private readonly LiveQuery _charges = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Player>(),
            ComponentType.ReadOnly<StrategicCharge>()));

        public StrategicCountdowns(VisualElement root, HUDPanelLayouts layouts)
            : base(root)
        {
            _rowTemplate = layouts.StrategicCountdownRow;
        }

        public override bool BlocksPointer => false;

        public override bool NeedsPlayer => false;

        public override void Refresh(HUDContext context)
        {
            var query = _charges.In(context.EntityManager);
            using var players = query.ToComponentDataArray<Player>(Allocator.Temp);
            using var charges = query.ToComponentDataArray<StrategicCharge>(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < players.Length; i++)
            {
                if (charges[i].HasAsset)
                {
                    FindRow(shown++).Show(players[i], charges[i], context);
                }
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].Label.SetHidden(i >= shown);
            }
        }

        private Row FindRow(int index)
        {
            if (index == _rows.Count)
            {
                var label = (Label)_rowTemplate.CloneRoot();
                Root.Add(label);
                _rows.Add(new Row(label));
            }

            return _rows[index];
        }

        /// <summary>One countdown line, rewritten only when its player, whole second or readiness changes.</summary>
        private sealed class Row
        {
            private int _owner = -1;
            private int _seconds = -1;
            private bool _ready;

            public Row(Label label)
            {
                Label = label;
            }

            public Label Label { get; }

            public void Show(in Player player, in StrategicCharge charge, HUDContext context)
            {
                var seconds = Mathf.CeilToInt(charge.Remaining);
                var samePlayer = player.Owner == _owner;
                var unchanged = samePlayer && charge.IsReady == _ready && (charge.IsReady || seconds == _seconds);
                if (unchanged)
                {
                    return;
                }

                if (!samePlayer)
                {
                    _owner = player.Owner;
                    Label.style.color = context.View.GetColor(player.Owner);
                }

                _seconds = seconds;
                _ready = charge.IsReady;
                var time = charge.IsReady ? HUDText.Text("hud.strategic-ready") : $"{seconds / 60}:{seconds % 60:00}";
                Label.text = $"{player.Name}  {time}";
                Label.EnableInClassList("hud-countdowns__row--ready", charge.IsReady);
            }
        }
    }
}
