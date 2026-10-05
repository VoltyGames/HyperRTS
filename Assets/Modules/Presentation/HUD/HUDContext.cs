using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
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

        private readonly LiveQuery _pointer = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PointerState>()));

        public MatchView View { get; } = new();

        /// <summary>Selected, visible entities this frame.</summary>
        public List<Entity> Selected { get; } = new();

        /// <summary>Changes whenever the selected set changes, so widgets know when to rebuild.</summary>
        public int SelectionHash { get; private set; }

        public EntityManager EntityManager => View.EntityManager;

        public bool Refresh()
        {
            Selected.Clear();
            if (!View.Refresh())
            {
                SelectionHash = 0;
                return false;
            }

            using var entities = _selected.In(EntityManager).ToEntityArray(Allocator.Temp);
            var hash = entities.Length;
            foreach (var entity in entities)
            {
                Selected.Add(entity);
                hash += (int)math.hash(new int2(entity.Index, entity.Version));
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

        public bool PrerequisitesMet(Entity prefab) =>
            !EntityManager.HasBuffer<Prerequisite>(prefab)
            || CompletedBuildings.MeetsPrerequisites(EntityManager.GetBuffer<Prerequisite>(prefab, true), View.Local.Faction,
                _completed.In(EntityManager));

        public void Issue(PlayerCommand command)
        {
            if (View.IsReady)
            {
                EntityManager.GetBuffer<PlayerCommand>(View.LocalPlayer).Add(command);
            }
        }

        /// <summary>Hands the building to the input layer, which moves the ghost and confirms placement.</summary>
        public void StartPlacement(Entity prefab)
        {
            var entity = SingletonUtility.Ensure<PlacementState>(EntityManager);
            EntityManager.SetComponentData(entity, new PlacementState { Active = true, Prefab = prefab });
        }

        public void SetPointerOverUI(bool over)
        {
            var entity = SingletonUtility.Ensure<PointerState>(EntityManager, _pointer.In(EntityManager));
            if (EntityManager.GetComponentData<PointerState>(entity).OverUI != over)
            {
                EntityManager.SetComponentData(entity, new PointerState { OverUI = over });
            }
        }
    }
}
