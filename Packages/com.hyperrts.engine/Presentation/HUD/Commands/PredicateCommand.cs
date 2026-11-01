using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>One button that issues a command, shown when some owned entity passes <c>test</c>.</summary>
    public sealed class PredicateCommand : ICommandSource
    {
        private readonly string _key;
        private readonly CommandType _type;
        private readonly int _argument;
        private readonly Func<EntityManager, Entity, bool> _test;

        public PredicateCommand(string key, CommandType type, int argument, Func<EntityManager, Entity, bool> test)
        {
            _key = key;
            _type = type;
            _argument = argument;
            _test = test;
        }

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            foreach (var entity in owned)
            {
                if (_test(context.EntityManager, entity))
                {
                    var command = new PlayerCommand { Type = _type, Argument = _argument };
                    entries.Add(new CommandEntry(HUDText.Text(_key), () => context.Issue(command)));
                    return;
                }
            }
        }

        public void Refresh(HUDContext context)
        {
        }
    }
}
