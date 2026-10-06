using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Upgrades;
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

        private readonly LiveQuery _queues = new(entityManager =>
            UpgradeRules.QueueQuery(Allocator.Temp).Build(entityManager));

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

        public bool CanAfford(Entity prefab) =>
            !EntityManager.HasBuffer<ResourceCost>(prefab)
            || ResourceMath.CanAfford(Stock, EntityManager.GetBuffer<ResourceCost>(prefab, true));

        /// <summary>This frame's completed buildings, shared by every button checking prerequisites.</summary>
        public CompletedBuildings SnapshotCompleted() => new(_completed.In(EntityManager), Allocator.Temp);

        public bool PrerequisitesMet(Entity prefab, in CompletedBuildings completed) =>
            !EntityManager.HasBuffer<Prerequisite>(prefab)
            || completed.MeetsPrerequisites(EntityManager.GetBuffer<Prerequisite>(prefab, true), View.Local.Faction);

        /// <summary>This frame's prefabs the local player has queued, shared by every research button.</summary>
        public NativeHashSet<Entity> SnapshotQueued()
        {
            var queued = new NativeHashSet<Entity>(8, Allocator.Temp);
            UpgradeRules.CollectQueued(EntityManager, _queues.In(EntityManager), View.Local.Faction, queued);
            return queued;
        }

        /// <summary>False for an upgrade the local player has researched or already queued somewhere.</summary>
        public bool CanQueueResearch(Entity prefab, NativeHashSet<Entity> queued) =>
            UpgradeRules.CanQueue(EntityManager, EntityManager.GetBuffer<ResearchedUpgrade>(View.LocalPlayer, true),
                queued, prefab);

        /// <summary>Arms a targeted command; the input layer issues it on the next world click.</summary>
        public void ArmCommand(CommandType type, int argument) =>
            Write(_pending, new PendingCommand { Type = type, Argument = argument });

        public void Issue(PlayerCommand command)
        {
            if (View.IsReady)
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
