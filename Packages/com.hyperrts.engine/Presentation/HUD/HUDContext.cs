using System.Collections.Generic;
using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.HUD
{
    /// <summary>What every HUD widget reads (match, selection) and the only way they write back to ECS.</summary>
    public sealed class HUDContext
    {
        private readonly LiveQuery _selected = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<EntityInfo>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly LiveQuery _completed = new(entityManager =>
            CompletedBuildings.Query(Allocator.Temp).Build(entityManager));

        private readonly LiveQuery _pointer = Singleton<PointerState>();
        private readonly LiveQuery _pending = Singleton<PendingCommand>();
        private readonly LiveQuery _placement = Singleton<PlacementState>();
        private readonly LiveQuery _cameraFocus = Singleton<CameraFocusRequest>();
        private readonly LiveQuery _hotkeys = Singleton<HotkeyPresses>();
        private readonly LiveQuery _registry = Singleton<PrefabRegistry>();

        private readonly LiveQuery _queues = new(entityManager =>
            ResearchRules.QueueQuery(Allocator.Temp).Build(entityManager));

        private readonly LiveQuery _limited = new(entityManager =>
            BuildCounts.Query(Allocator.Temp).Build(entityManager));

        public MatchView View { get; private set; }

        /// <summary>Selected, visible entities this frame.</summary>
        public List<Entity> Selected { get; } = new();

        /// <summary>
        /// Changes whenever the selected set, an owner or a construction state in it changes, so widgets know when to
        /// rebuild (a captured or finished building gets a new card).
        /// </summary>
        public int SelectionHash { get; private set; }

        public EntityManager EntityManager => View.EntityManager;

        /// <summary>Reads this frame's selection through <paramref name="view"/>; false until it is ready.</summary>
        public bool Refresh(MatchView view)
        {
            View = view;
            Selected.Clear();
            SelectionHash = 0;
            if (!view.IsReady)
            {
                return false;
            }

            using var entities = _selected.In(EntityManager).ToEntityArray(Allocator.Temp);
            var hash = entities.Length;
            foreach (var entity in entities)
            {
                Selected.Add(entity);
                hash += StateHash(entity);
            }

            SelectionHash = hash;
            return true;
        }

        public bool IsOwned(Entity entity) =>
            EntityManager.HasComponent<Faction>(entity)
            && EntityManager.GetComponentData<Faction>(entity).Value == View.Local.Faction;

        public DynamicBuffer<ResourceStock> Stock => EntityManager.GetBuffer<ResourceStock>(View.LocalPlayer, true);

        /// <summary>The local player as a buyer this frame, shared by every button that checks a purchase.</summary>
        public Buyer SnapshotBuyer() =>
            Buyer.Snapshot(EntityManager, _queues.In(EntityManager), _limited.In(EntityManager), View.Local.Faction,
                new CompletedBuildings(_completed.In(EntityManager), Allocator.Temp),
                EntityManager.GetBuffer<CompletedResearch>(View.LocalPlayer, true));

        /// <summary>The letters and digits pressed last frame while gameplay input was live; none without the singleton.</summary>
        public HotkeyPresses Hotkeys =>
            _hotkeys.In(EntityManager).TryGetSingleton(out HotkeyPresses presses) ? presses : default;

        public PurchaseRefusal FindRefusal(Entity prefab, in Buyer buyer) =>
            PurchaseRules.FindRefusal(EntityManager, prefab, buyer, Stock.AsNativeArray());

        /// <summary>Adds the prefabs of <paramref name="prefab"/>'s unmet prerequisites to <paramref name="missing"/>.</summary>
        public void FindMissingPrerequisites(Entity prefab, in Buyer buyer, List<Entity> missing)
        {
            if (!_registry.In(EntityManager).TryGetSingleton(out PrefabRegistry registry))
            {
                return;
            }

            foreach (var prerequisite in EntityManager.GetBuffer<Prerequisite>(prefab, true))
            {
                var required = registry.Find(prerequisite.TypeId);
                var unmet = !buyer.Buildings.IsMet(buyer.Faction, prerequisite.TypeId, buyer.Research);
                if (unmet && required != Entity.Null)
                {
                    missing.Add(required);
                }
            }
        }

        /// <summary>
        /// How many <paramref name="prefab"/> the <paramref name="producers"/> have queued, and the furthest one in work.
        /// </summary>
        public QueuedOption FindQueued(IReadOnlyList<Entity> producers, Entity prefab)
        {
            var count = 0;
            var progress = QueuedOption.Idle;
            foreach (var producer in producers)
            {
                var queue = EntityManager.GetBuffer<ProductionQueueItem>(producer, true);
                for (var i = 0; i < queue.Length; i++)
                {
                    if (queue[i].Prefab != prefab)
                    {
                        continue;
                    }

                    count++;
                    if (i == 0)
                    {
                        progress = math.max(progress, HeadProgress(producer, prefab));
                    }
                }
            }

            return new QueuedOption(count, progress);
        }

        /// <summary>Share of <paramref name="prefab"/>, at the head of <paramref name="producer"/>'s queue, built.</summary>
        public float HeadProgress(Entity producer, Entity prefab)
        {
            var elapsed = EntityManager.GetComponentData<Producer>(producer).Elapsed;
            var buildTime = EntityManager.HasComponent<Producible>(prefab)
                ? EntityManager.GetComponentData<Producible>(prefab).BuildTime
                : 0f;
            return buildTime > 0f ? math.saturate(elapsed / buildTime) : 1f;
        }

        /// <summary>True while <paramref name="entity"/> is offline because its owner's power is low.</summary>
        public bool IsUnpowered(Entity entity) =>
            EntityManager.HasComponent<Offline>(entity)
            && EntityManager.IsComponentEnabled<Offline>(entity)
            && (EntityManager.GetComponentData<Offline>(entity).Reasons & OfflineReason.Unpowered) != 0;

        /// <summary>Starts targeting a command; the input layer issues it on the next world click.</summary>
        public void BeginTargeting(CommandType type, int argument) =>
            Write(_pending, new PendingCommand { Type = type, Argument = argument });

        public void Issue(PlayerCommand command)
        {
            if (View.HasLocalPlayer)
            {
                EntityManager.GetBuffer<PlayerCommand>(View.LocalPlayer).Add(command);
            }
        }

        /// <summary>Hands the building to the input layer, which moves the ghost and confirms placement.</summary>
        public void StartPlacement(Entity prefab) =>
            Write(_placement, new PlacementState { Active = true, Prefab = prefab });

        /// <summary>Asks the camera to centre on a ground point.</summary>
        public void FocusCamera(float3 point) =>
            Write(_cameraFocus, new CameraFocusRequest { Pending = true, Point = point });

        public void SetPointerOverUI(bool over)
        {
            if (!View.IsLive)
            {
                return;
            }

            var query = _pointer.In(EntityManager);
            var changed = query.TryGetSingleton(out PointerState pointer) && pointer.OverUI != over;
            if (changed)
            {
                query.SetSingleton(new PointerState { OverUI = over });
            }
        }

        private static LiveQuery Singleton<T>() where T : unmanaged, IComponentData =>
            new(entityManager => entityManager.CreateEntityQuery(ComponentType.ReadWrite<T>()));

        // The singletons come from ClientSingletonSystem, so a world without it (headless) simply ignores the HUD.
        private void Write<T>(LiveQuery singleton, T value) where T : unmanaged, IComponentData
        {
            if (!View.IsLive)
            {
                return;
            }

            var query = singleton.In(EntityManager);
            if (query.HasSingleton<T>())
            {
                query.SetSingleton(value);
            }
        }

        private int StateHash(Entity entity)
        {
            var faction = 0;
            if (EntityManager.HasComponent<Faction>(entity))
            {
                faction = EntityManager.GetComponentData<Faction>(entity).Value;
            }

            var building = EntityManager.HasEnabled<ConstructionProgress>(entity) ? 1 : 0;
            return (int)math.hash(new int4(entity.Index, entity.Version, faction, building));
        }
    }
}
