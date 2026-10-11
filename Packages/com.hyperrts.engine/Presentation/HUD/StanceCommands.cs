using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>One button per stance for armed units, the stance they all share highlighted.</summary>
    public sealed class StanceCommands : ICommandSource
    {
        private const string ActiveClass = "hud-command--active";
        private static readonly Stance[] Stances = (Stance[])Enum.GetValues(typeof(Stance));

        private readonly List<(CommandButton Button, Stance Stance)> _buttons = new();
        private readonly List<Entity> _armed = new();

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _buttons.Clear();
            _armed.Clear();
            foreach (var entity in owned)
            {
                if (context.EntityManager.HasComponent<CombatStance>(entity))
                {
                    _armed.Add(entity);
                }
            }

            if (_armed.Count == 0)
            {
                return;
            }

            foreach (var stance in Stances)
            {
                var command = new PlayerCommand { Type = CommandType.SetStance, Argument = (int)stance };
                Action invoke = () => context.Issue(command);
                var button = new CommandButton(HUDText.StanceName(stance), invoke);
                button.AddToClassList("hud-command--stance");
                _buttons.Add((button, stance));
                entries.Add(new CommandEntry(button, invoke));
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
