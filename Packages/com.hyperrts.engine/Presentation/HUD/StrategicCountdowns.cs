using System.Collections.Generic;
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
        private readonly List<Label> _rows = new();
        private World _world;
        private EntityQuery _charges;

        public StrategicCountdowns(VisualElement root)
            : base(root)
        {
        }

        public override bool BlocksPointer => false;

        public override bool NeedsPlayer => false;

        public override void Refresh(HUDContext context)
        {
            var entityManager = context.EntityManager;
            if (_world != entityManager.World)
            {
                _world = entityManager.World;
                _charges = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Player>(),
                    ComponentType.ReadOnly<StrategicCharge>());
            }

            using var players = _charges.ToComponentDataArray<Player>(Allocator.Temp);
            using var charges = _charges.ToComponentDataArray<StrategicCharge>(Allocator.Temp);
            var shown = 0;
            for (var i = 0; i < players.Length; i++)
            {
                if (charges[i].HasAsset)
                {
                    Show(Row(shown++), players[i], charges[i], context);
                }
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].SetHidden(i >= shown);
            }
        }

        private Label Row(int index)
        {
            if (index == _rows.Count)
            {
                _rows.Add(HUDElements.Text("", "hud-countdowns__row", Root));
            }

            return _rows[index];
        }

        private static void Show(Label row, in Player player, in StrategicCharge charge, HUDContext context)
        {
            var time = charge.IsReady ? HUDText.Text("hud.strategic-ready") : Clock(charge.Remaining);
            row.text = $"{player.Name}  {time}";
            row.style.color = context.View.GetColor(player.Faction);
            row.EnableInClassList("hud-countdowns__row--ready", charge.IsReady);
        }

        private static string Clock(float seconds)
        {
            var whole = Mathf.CeilToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
