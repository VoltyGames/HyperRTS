using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Squads;
using Unity.Entities;
using Unity.Transforms;

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

        private readonly LiveQuery _retreatPoints = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<RetreatPoint>(), ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Dead>(), ComponentType.Exclude<Offline>()));

        private readonly LiveQuery _reinforcePoints = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ReinforcePoint>(), ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>(), ComponentType.Exclude<Dead>(), ComponentType.Exclude<Offline>()));

        private readonly List<Entity> _squads = new();
        private CommandButton _retreat;
        private CommandButton _reinforce;
        private SquadRefusal _retreatReason;
        private SquadRefusal _reinforceReason;

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
            var relations = context.View.Relations;
            var reinforcePoints = SquadPoints.From<ReinforcePoint>(_reinforcePoints.In(entityManager));
            var allRetreating = true;
            var allReinforcing = true;
            var reinforceReason = SquadRefusal.FullStrength;
            foreach (var squad in _squads)
            {
                if (!entityManager.Exists(squad))
                {
                    continue;
                }

                allRetreating &= entityManager.HasEnabled<Retreating>(squad);
                if (entityManager.HasEnabled<Reinforcing>(squad))
                {
                    continue;
                }

                allReinforcing = false;
                var here = entityManager.GetComponentData<LocalTransform>(squad).Position;
                var refusal = SquadOrderRule.FindReinforceRefusal(entityManager.GetBuffer<SquadMember>(squad, true),
                    reinforcePoints.Covers(here, faction, relations), rules, Cost(entityManager, squad), context.Stock);
                reinforceReason = NearestToAble(reinforceReason, refusal);
            }

            var retreatPoints = SquadPoints.From<RetreatPoint>(_retreatPoints.In(entityManager));
            _retreatReason = SquadOrderRule.FindRetreatRefusal(retreatPoints.HasAllied(faction, relations));
            _reinforceReason = allReinforcing ? SquadRefusal.None : reinforceReason;
            _retreat.ShowSquadRefusal(_retreatReason);
            _reinforce.ShowSquadRefusal(_reinforceReason);
            _retreat.EnableInClassList(ActiveClass, allRetreating);
            _reinforce.EnableInClassList(ActiveClass, allReinforcing);
        }

        // Selected squads share one button, so it shows the reason closest to able; any squad that can act clears it.
        private static SquadRefusal NearestToAble(SquadRefusal shown, SquadRefusal next)
        {
            if (shown == SquadRefusal.None || next == SquadRefusal.None)
            {
                return SquadRefusal.None;
            }

            return shown > next ? shown : next;
        }

        private static DynamicBuffer<ResourceCost> Cost(EntityManager entityManager, Entity squad) =>
            entityManager.HasBuffer<ResourceCost>(squad)
                ? entityManager.GetBuffer<ResourceCost>(squad, true)
                : default;

        private void DescribeRetreat(HUDTooltip tooltip)
        {
            tooltip.AddTitle(HUDText.Text("command.retreat"));
            tooltip.AddLine(HUDText.Text("tooltip.retreat"));
            AddWarning(tooltip, _retreatReason, "tooltip.no-retreat-point");
        }

        private void DescribeReinforce(HUDContext context, HUDTooltip tooltip)
        {
            tooltip.AddTitle(HUDText.Text("command.reinforce"));
            tooltip.AddLine(HUDText.Text("tooltip.reinforce"));
            if (context.TryGetSquadRules(out var rules))
            {
                AddSoldierCost(context.EntityManager, tooltip, rules);
                tooltip.AddStat(HUDText.Text("tooltip.reinforce-time"), HUDText.Seconds(rules.ReinforceTime));
            }

            AddWarning(tooltip, _reinforceReason, "tooltip.no-reinforce-point");
        }

        private static void AddWarning(HUDTooltip tooltip, SquadRefusal reason, string noPointKey)
        {
            switch (reason)
            {
                case SquadRefusal.NoPoint:
                    tooltip.AddWarning(HUDText.Text(noPointKey));
                    break;
                case SquadRefusal.FullStrength:
                    tooltip.AddWarning(HUDText.Text("tooltip.full-strength"));
                    break;
                case SquadRefusal.TooPoor:
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

                var share = rules.ReinforceShare(entityManager.GetBuffer<SquadMember>(squad, true).Length);
                foreach (var cost in entityManager.GetBuffer<ResourceCost>(squad, true))
                {
                    tooltip.AddStat(HUDText.ResourceName(cost.Type.Value), ((int)(cost.Amount * share)).ToString());
                }

                return;
            }
        }
    }
}
