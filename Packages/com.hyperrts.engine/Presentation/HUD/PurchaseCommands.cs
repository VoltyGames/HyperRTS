using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using Unity.Entities;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>Build, train and research buttons, greyed out while the purchase rules refuse them.</summary>
    public sealed class PurchaseCommands : ICommandSource
    {
        private readonly List<(CommandButton Button, Entity Prefab)> _buttons = new();

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _buttons.Clear();
            var builds = new List<Entity>();
            var products = new List<Entity>();
            var entityManager = context.EntityManager;
            foreach (var entity in owned)
            {
                Collect<BuildOption>(entityManager, entity, builds);
                Collect<ProductionOption>(entityManager, entity, products);
            }

            foreach (var prefab in builds)
            {
                Add(context, entries, prefab, () => context.StartPlacement(prefab));
            }

            foreach (var prefab in products)
            {
                Add(context, entries, prefab,
                    () => context.Issue(new PlayerCommand { Type = CommandType.Produce, Prefab = prefab }));
            }
        }

        public void Refresh(HUDContext context)
        {
            if (_buttons.Count == 0)
            {
                return;
            }

            var buyer = context.SnapshotBuyer();
            foreach (var (button, prefab) in _buttons)
            {
                button.SetEnabled(context.CanBuy(prefab, buyer));
            }
        }

        private void Add(HUDContext context, List<CommandEntry> entries, Entity prefab, System.Action invoke)
        {
            var button = CommandButton.ForPrefab(context.EntityManager, prefab, invoke);
            _buttons.Add((button, prefab));
            entries.Add(new CommandEntry(button, invoke));
        }

        private static void Collect<T>(EntityManager entityManager, Entity entity, List<Entity> prefabs)
            where T : unmanaged, IBufferElementData, IPrefabOption
        {
            if (!entityManager.HasBuffer<T>(entity))
            {
                return;
            }

            foreach (var option in entityManager.GetBuffer<T>(entity, true))
            {
                if (option.Prefab != Entity.Null && !prefabs.Contains(option.Prefab))
                {
                    prefabs.Add(option.Prefab);
                }
            }
        }
    }
}
