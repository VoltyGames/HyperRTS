using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using HyperRTS.Presentation.Common;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// Retreat and Reinforce for selected squads. Retreat needs a friendly retreat point; Reinforce needs a dead
    /// soldier, a friendly reinforce point in range and the price of one soldier. A button that can't act stays on the
    /// card, refused, and its tooltip says why.
    /// </summary>
    public sealed class SquadCommands : ICommandSource
    {
        private const string ActiveClass = "hud-command--active";

        private readonly SquadPoints _points = new();
        private readonly List<Entity> _squads = new();
        private CommandButton _retreat;
        private CommandButton _reinforce;
        private Refusal _retreatReason;
        private Refusal _reinforceReason;

        /// <summary>Why a button can't act now.</summary>
        public enum Refusal : byte
        {
            None,
            NoPoint,
            FullStrength,
            TooPoor,
        }

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _squads.Clear();
            _retreat = null;
            _reinforce = null;
            foreach (var entity in owned)
            {
                if (context.EntityManager.HasBuffer<SquadMember>(entity))
                {
                    _squads.Add(entity);
                }
            }

            if (_squads.Count == 0)
            {
                return;
            }

            var retreat = new CommandEntry(HUDText.Text("command.retreat"),
                () => context.Issue(new PlayerCommand { Type = CommandType.Retreat })) { Hotkey = 'R' };
            _retreat = retreat.Button;
            _retreat.Description = tooltip => DescribeRetreat(tooltip);
            var reinforce = new CommandEntry(HUDText.Text("command.reinforce"),
                () => context.Issue(new PlayerCommand { Type = CommandType.Reinforce })) { Hotkey = 'T' };
            _reinforce = reinforce.Button;
            _reinforce.Description = tooltip => DescribeReinforce(context, tooltip);
            entries.Add(retreat);
            entries.Add(reinforce);
        }

        public void Refresh(HUDContext context)
        {
            if (_squads.Count == 0 || !context.TryGetSquadRules(out var rules))
            {
                return;
            }

            var entityManager = context.EntityManager;
            var faction = context.View.Local.Faction;
            var hasRetreatPoint = _points.HasRetreat(context, faction);
            using var reinforcePoints = _points.FindReinforce(context, faction);
            var anyDead = false;
            var anyInRange = false;
            var anyAffordable = false;
            var allRetreating = true;
            var allReinforcing = true;
            foreach (var squad in _squads)
            {
                if (!entityManager.Exists(squad))
                {
                    continue;
                }

                var members = entityManager.GetBuffer<SquadMember>(squad, true);
                var retreating = entityManager.GetComponentData<Suppression>(squad).Retreating;
                var reinforcing = entityManager.HasEnabled<Reinforcing>(squad);
                allRetreating &= retreating;
                allReinforcing &= reinforcing;
                if (reinforcing || SquadMember.CountLiving(members) == members.Length)
                {
                    continue;
                }

                anyDead = true;
                if (SquadPoints.IsInRange(entityManager, squad, reinforcePoints))
                {
                    anyInRange = true;
                    anyAffordable |= CanPay(context, squad, rules.ReinforceCost / members.Length);
                }
            }

            _retreatReason = hasRetreatPoint ? Refusal.None : Refusal.NoPoint;
            _reinforceReason = FindReinforceReason(anyDead, anyInRange, anyAffordable, allReinforcing);
            _retreat.ShowRefusal(ToRefusal(_retreatReason));
            _reinforce.ShowRefusal(ToRefusal(_reinforceReason));
            _retreat.EnableInClassList(ActiveClass, allRetreating);
            _reinforce.EnableInClassList(ActiveClass, allReinforcing);
        }

        /// <summary>The refusal for a reinforce button, given what the selected squads allow; none while any can start or all are working.</summary>
        public static Refusal FindReinforceReason(bool anyDead, bool anyInRange, bool anyAffordable, bool allReinforcing)
        {
            if (allReinforcing)
            {
                return Refusal.None;
            }

            if (!anyDead)
            {
                return Refusal.FullStrength;
            }

            if (!anyInRange)
            {
                return Refusal.NoPoint;
            }

            return anyAffordable ? Refusal.None : Refusal.TooPoor;
        }

        private static PurchaseRefusal ToRefusal(Refusal reason) => reason switch
        {
            Refusal.NoPoint => PurchaseRefusal.MissingPrerequisite,
            Refusal.FullStrength => PurchaseRefusal.AtLimit,
            Refusal.TooPoor => PurchaseRefusal.CannotAfford,
            _ => PurchaseRefusal.None,
        };

        private void DescribeRetreat(HUDTooltip tooltip)
        {
            tooltip.AddTitle(HUDText.Text("command.retreat"));
            tooltip.AddLine(HUDText.Text("tooltip.retreat"));
            if (_retreatReason == Refusal.NoPoint)
            {
                tooltip.AddWarning(HUDText.Text("tooltip.no-retreat-point"));
            }
        }

        private void DescribeReinforce(HUDContext context, HUDTooltip tooltip)
        {
            tooltip.AddTitle(HUDText.Text("command.reinforce"));
            tooltip.AddLine(HUDText.Text("tooltip.reinforce"));
            var entityManager = context.EntityManager;
            if (context.TryGetSquadRules(out var rules))
            {
                AddSoldierCost(entityManager, tooltip, rules);
                tooltip.AddStat(HUDText.Text("tooltip.reinforce-time"), HUDText.Seconds(rules.ReinforceTime));
            }

            switch (_reinforceReason)
            {
                case Refusal.NoPoint:
                    tooltip.AddWarning(HUDText.Text("tooltip.no-reinforce-point"));
                    break;
                case Refusal.FullStrength:
                    tooltip.AddWarning(HUDText.Text("tooltip.full-strength"));
                    break;
                case Refusal.TooPoor:
                    tooltip.AddWarning(HUDText.Text("tooltip.cannot-afford"));
                    break;
            }
        }

        // The first squad stands for the selection: its price and size set the share one soldier costs.
        private void AddSoldierCost(EntityManager entityManager, HUDTooltip tooltip, in SquadRules rules)
        {
            foreach (var squad in _squads)
            {
                if (!entityManager.Exists(squad) || !entityManager.HasBuffer<ResourceCost>(squad))
                {
                    continue;
                }

                var share = rules.ReinforceCost / entityManager.GetBuffer<SquadMember>(squad, true).Length;
                foreach (var cost in entityManager.GetBuffer<ResourceCost>(squad, true))
                {
                    tooltip.AddStat(HUDText.ResourceName(cost.Type.Value), ((int)(cost.Amount * share)).ToString());
                }

                return;
            }
        }

        private static bool CanPay(HUDContext context, Entity squad, float share)
        {
            var entityManager = context.EntityManager;
            return !entityManager.HasBuffer<ResourceCost>(squad)
                   || ResourceMath.CanAfford(context.Stock, entityManager.GetBuffer<ResourceCost>(squad, true), share);
        }
    }
}
