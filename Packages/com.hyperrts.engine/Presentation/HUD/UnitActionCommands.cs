using System.Collections.Generic;
using HyperRTS.Simulation.Air;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Transport;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Return to base, unload and sell, each shown when some owned entity can do it.</summary>
    public sealed class UnitActionCommands : ICommandSource
    {
        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            var entityManager = context.EntityManager;
            var canReturn = false;
            var canUnload = false;
            var canSell = false;
            foreach (var entity in owned)
            {
                canReturn |= entityManager.HasComponent<HomePad>(entity);
                canUnload |= entityManager.HasComponent<Container>(entity);
                canSell |= entityManager.HasComponent<BuildingTag>(entity);
            }

            Add(context, entries, canReturn, "command.return", CommandType.ReturnToBase, 0);
            Add(context, entries, canUnload, "command.unload", CommandType.Unload, -1);
            Add(context, entries, canSell, "command.sell", CommandType.Sell, 0);
        }

        public void Refresh(HUDContext context)
        {
        }

        private static void Add(HUDContext context, List<CommandEntry> entries, bool shown, string key,
            CommandType type, int argument)
        {
            if (!shown)
            {
                return;
            }

            var command = new PlayerCommand { Type = type, Argument = argument };
            entries.Add(new CommandEntry(HUDText.Text(key), () => context.Issue(command)));
        }
    }
}
