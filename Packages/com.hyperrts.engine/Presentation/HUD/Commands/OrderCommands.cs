using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Deploy;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Transport;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>
    /// Buttons for plain orders when some owned entity can take them: return, unload, deploy, pack up, sell and
    /// reverse move and fire mission, and a stance each for armed units with the one they all share highlighted.
    /// </summary>
    public sealed class OrderCommands : ICommandSource
    {
        private const string ActiveClass = "hud-command--active";
        private static readonly Stance[] Stances = (Stance[])Enum.GetValues(typeof(Stance));

        private static readonly (CommandType Order, int Argument)[] Plain =
        {
            (CommandType.ReturnToBase, 0), (CommandType.Unload, -1), (CommandType.Deploy, 0),
            (CommandType.PackUp, 0), (CommandType.Sell, 0), (CommandType.ReverseMove, 0), (CommandType.FireMission, 0),
        };

        private readonly CommandKit _kit;
        private readonly List<(CommandButton Button, Stance Stance)> _buttons = new();
        private readonly List<Entity> _armed = new();

        public OrderCommands(CommandKit kit)
        {
            _kit = kit;
        }

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _buttons.Clear();
            _armed.Clear();
            foreach (var (order, argument) in Plain)
            {
                AddOrder(context, owned, entries, order, argument);
            }

            foreach (var entity in owned)
            {
                if (context.EntityManager.HasComponent<CombatStance>(entity))
                {
                    _armed.Add(entity);
                }
            }

            foreach (var stance in Stances)
            {
                var button = AddOrder(context, owned, entries, CommandType.SetStance, (int)stance);
                if (button != null)
                {
                    _buttons.Add((button, stance));
                }
            }
        }

        public void Refresh(HUDContext context)
        {
            var common = CommonStance(context.EntityManager);
            foreach (var (button, stance) in _buttons)
            {
                button.EnableInClassList(ActiveClass, common == stance);
            }
        }

        // The first owned entity that can take the order stands for the selection's card.
        private CommandButton AddOrder(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries,
            CommandType order, int argument)
        {
            var entityManager = context.EntityManager;
            foreach (var entity in owned)
            {
                if (!CanTake(entityManager, entity, order))
                {
                    continue;
                }

                var spec = _kit.Catalog.FindOrder(EntityInfo.GetTypeId(entityManager, entity), order, argument);
                if (spec == null)
                {
                    return null;
                }

                var entry = _kit.Create(spec, "", null, () => Press(context, order, argument));
                entries.Add(entry);
                return entry.Button;
            }

            return null;
        }

        // An order that needs a place waits for the next world click; the rest go at once.
        private static void Press(HUDContext context, CommandType order, int argument)
        {
            if (order is CommandType.ReverseMove or CommandType.FireMission)
            {
                context.BeginTargeting(order, argument);
                return;
            }

            context.Issue(new PlayerCommand { Type = order, Argument = argument });
        }

        private static bool CanTake(EntityManager entityManager, Entity entity, CommandType order) => order switch
        {
            CommandType.ReturnToBase => entityManager.HasComponent<HomePad>(entity),
            CommandType.Unload => entityManager.HasComponent<Container>(entity),
            CommandType.Deploy or CommandType.PackUp => entityManager.HasComponent<Deployable>(entity),
            CommandType.Sell => BuildingRules.IsSellable(entityManager, entity),
            CommandType.SetStance => entityManager.HasComponent<CombatStance>(entity),
            CommandType.FireMission => entityManager.HasComponent<PointAim>(entity),
            CommandType.ReverseMove => entityManager.HasComponent<NavAgent>(entity) &&
                                       entityManager.GetComponentData<NavAgent>(entity).CanReverse,
            _ => false,
        };

        /// <summary>The stance every armed unit shares, or null when they differ.</summary>
        private Stance? CommonStance(EntityManager entityManager)
        {
            Stance? common = null;
            foreach (var entity in _armed)
            {
                if (!entityManager.Exists(entity))
                {
                    return null;
                }

                var stance = entityManager.GetComponentData<CombatStance>(entity).Value;
                if (common.HasValue && common != stance)
                {
                    return null;
                }

                common = stance;
            }

            return common;
        }
    }
}
