using System.Collections.Generic;
using HyperRTS.Client.Common;
using HyperRTS.Simulation.Airdrops;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>The map-wide strategic asset countdowns: one row per player holding one, in the owner's colour.</summary>
    public sealed class StrategicCountdowns : HUDPanel
    {
        private readonly List<Row> _rows = new();

        private readonly LiveQuery _charges = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Player>(),
            ComponentType.ReadOnly<StrategicCharge>()));

        public StrategicCountdowns(VisualElement root)
            : base(root)
        {
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
                _rows.Add(new Row(HUDElements.Text("", "hud-countdowns__row", Root)));
            }

            return _rows[index];
        }

        /// <summary>One countdown line, rewritten only when its player, whole second or readiness changes.</summary>
        private sealed class Row
        {
            private int _faction = -1;
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
                var samePlayer = player.Faction == _faction;
                var unchanged = samePlayer && charge.IsReady == _ready && (charge.IsReady || seconds == _seconds);
                if (unchanged)
                {
                    return;
                }

                if (!samePlayer)
                {
                    _faction = player.Faction;
                    Label.style.color = context.View.GetColor(player.Faction);
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
