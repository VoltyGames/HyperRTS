using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>
    /// Build, train and research cards, showing why the purchase rules refuse one, what the selected producers have
    /// queued and whether low power slows them. A builder's advanced options go on the card's second page.
    /// </summary>
    public sealed class PurchaseCommands : ICommandSource
    {
        private readonly List<Option> _options = new();
        private readonly List<Entity> _producers = new();
        private readonly HashSet<(Entity Producer, Entity Prefab)> _offers = new();
        private readonly HashSet<Entity> _unpowered = new();
        private readonly Dictionary<Entity, QueuedOption> _queued = new();

        private static QueuedOption Idle => new(0, QueuedOption.Idle);

        public void Build(HUDContext context, IReadOnlyList<Entity> owned, List<CommandEntry> entries)
        {
            _options.Clear();
            _producers.Clear();
            _offers.Clear();
            var builds = new List<Entity>();
            var advancedBuilds = new List<Entity>();
            var products = new List<Entity>();
            var entityManager = context.EntityManager;
            foreach (var entity in owned)
            {
                CollectBuilds(entityManager, entity, builds, advancedBuilds);
                Collect<ProductionOption>(entityManager, entity, products);
            }

            foreach (var prefab in builds)
            {
                Add(context, entries, prefab, new List<Entity>(), () => context.StartPlacement(prefab));
            }

            foreach (var prefab in advancedBuilds)
            {
                Add(context, entries, prefab, new List<Entity>(), () => context.StartPlacement(prefab)).Page = 1;
            }

            foreach (var prefab in products)
            {
                Add(context, entries, prefab, FindProducers(entityManager, owned, prefab),
                    () => context.Issue(new PlayerCommand { Type = CommandType.Produce, Prefab = prefab }));
            }
        }

        public void Refresh(HUDContext context)
        {
            if (_options.Count == 0)
            {
                return;
            }

            ReadProducers(context);
            var entityManager = context.EntityManager;
            var buyer = context.SnapshotBuyer();
            foreach (var option in _options)
            {
                var button = option.Button;
                button.ShowRefusal(context.FindRefusal(option.Prefab, buyer));
                button.ShowQueue(FindQueued(option));
                button.ShowUnpowered(HasUnpowered(option.Producers));
                if (entityManager.HasComponent<BuildLimit>(option.Prefab))
                {
                    button.ShowLimit(buyer.Counts.Count(entityManager, option.Prefab),
                        entityManager.GetComponentData<BuildLimit>(option.Prefab).Max);
                }
            }
        }

        // One pass over the selected producers' queues, counting each prefab only where that producer offers it.
        private void ReadProducers(HUDContext context)
        {
            _unpowered.Clear();
            _queued.Clear();
            foreach (var producer in _producers)
            {
                if (context.IsUnpowered(producer))
                {
                    _unpowered.Add(producer);
                }

                var queue = context.EntityManager.GetBuffer<ProductionQueueItem>(producer, true);
                for (var i = 0; i < queue.Length; i++)
                {
                    var prefab = queue[i].Prefab;
                    if (!_offers.Contains((producer, prefab)))
                    {
                        continue;
                    }

                    var queued = _queued.TryGetValue(prefab, out var found) ? found : Idle;
                    var progress = i == 0
                        ? math.max(queued.Progress, context.HeadProgress(producer, prefab))
                        : queued.Progress;
                    _queued[prefab] = new QueuedOption(queued.Count + 1, progress);
                }
            }
        }

        // A placed building has no producers, so it never shows a queue even if a factory also trains its prefab.
        private QueuedOption FindQueued(Option option) =>
            option.Producers.Count > 0 && _queued.TryGetValue(option.Prefab, out var queued) ? queued : Idle;

        private bool HasUnpowered(List<Entity> producers)
        {
            foreach (var producer in producers)
            {
                if (_unpowered.Contains(producer))
                {
                    return true;
                }
            }

            return false;
        }

        private CommandEntry Add(HUDContext context, List<CommandEntry> entries, Entity prefab,
            List<Entity> producers, System.Action invoke)
        {
            var button = new CommandButton().BindPrefab(context.EntityManager, prefab, invoke);
            button.Description = tooltip => Describe(context, prefab, tooltip);
            _options.Add(new Option(button, prefab, producers));
            var entry = new CommandEntry(button, invoke);
            entries.Add(entry);
            return entry;
        }

        /// <summary>Name, description, cost, build time, population, power and missing prerequisites.</summary>
        private static void Describe(HUDContext context, Entity prefab, HUDTooltip tooltip)
        {
            var entityManager = context.EntityManager;
            tooltip.AddTitle(HUDText.EntityName(entityManager, prefab));
            var description = HUDText.EntityDescription(entityManager, prefab);
            if (description != null)
            {
                tooltip.AddLine(description);
            }

            if (entityManager.HasBuffer<ResourceCost>(prefab))
            {
                tooltip.AddCosts(entityManager.GetBuffer<ResourceCost>(prefab, true));
            }

            if (entityManager.HasComponent<Producible>(prefab))
            {
                var producible = entityManager.GetComponentData<Producible>(prefab);
                tooltip.AddStat(HUDText.Text("tooltip.build-time"), HUDText.Seconds(producible.BuildTime));
                if (producible.Population > 0)
                {
                    tooltip.AddStat(HUDText.Text("tooltip.population"), producible.Population.ToString());
                }
            }

            if (entityManager.HasComponent<PowerSupply>(prefab))
            {
                var power = entityManager.GetComponentData<PowerSupply>(prefab).Amount;
                tooltip.AddStat(HUDText.Text("tooltip.power"), power.ToString("+0;-0;0"));
            }

            var missing = new List<Entity>();
            context.FindMissingPrerequisites(prefab, context.SnapshotBuyer(), missing);
            foreach (var required in missing)
            {
                tooltip.AddWarning($"{HUDText.Text("tooltip.requires")} {HUDText.EntityName(entityManager, required)}");
            }
        }

        private List<Entity> FindProducers(EntityManager entityManager, IReadOnlyList<Entity> owned, Entity prefab)
        {
            var producers = new List<Entity>();
            foreach (var entity in owned)
            {
                var offers = entityManager.HasBuffer<ProductionQueueItem>(entity)
                             && entityManager.HasBuffer<ProductionOption>(entity)
                             && ProductionRules.HasOption(entityManager.GetBuffer<ProductionOption>(entity, true),
                                 prefab);
                if (!offers)
                {
                    continue;
                }

                producers.Add(entity);
                _offers.Add((entity, prefab));
                if (!_producers.Contains(entity))
                {
                    _producers.Add(entity);
                }
            }

            return producers;
        }

        // The first builder to list a prefab decides its page, so a mixed selection shows each option once.
        private static void CollectBuilds(EntityManager entityManager, Entity entity, List<Entity> builds,
            List<Entity> advancedBuilds)
        {
            if (!entityManager.HasBuffer<BuildOption>(entity))
            {
                return;
            }

            foreach (var option in entityManager.GetBuffer<BuildOption>(entity, true))
            {
                var listed = option.Prefab == Entity.Null || builds.Contains(option.Prefab)
                             || advancedBuilds.Contains(option.Prefab);
                if (!listed)
                {
                    (option.Advanced ? advancedBuilds : builds).Add(option.Prefab);
                }
            }
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

        private readonly struct Option
        {
            public Option(CommandButton button, Entity prefab, List<Entity> producers)
            {
                Button = button;
                Prefab = prefab;
                Producers = producers;
            }

            public CommandButton Button { get; }
            public Entity Prefab { get; }

            /// <summary>Selected producers that offer it; none for a building a builder places.</summary>
            public List<Entity> Producers { get; }
        }
    }
}
