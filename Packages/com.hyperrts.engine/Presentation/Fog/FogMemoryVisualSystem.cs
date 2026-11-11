using HyperRTS.Presentation.Rendering;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Draws each <see cref="FogMemory"/> memory as a team-coloured, visual-only copy of its building.</summary>
    // Main thread: copies are rare, and building a type's visual prefab reads component types through reflection.
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(TeamColorSystem))]
    public partial struct FogMemoryVisualSystem : ISystem
    {
        private NativeHashMap<Entity, Entity> _visualPrefabs;
        private EntityQuery _undrawn;
        private EntityQuery _forgotten;

        public void OnCreate(ref SystemState state)
        {
            _visualPrefabs = new NativeHashMap<Entity, Entity>(16, Allocator.Persistent);
            _undrawn = SystemAPI.QueryBuilder().WithAll<FogMemory>().WithNone<FogMemoryVisual>().Build();
            _forgotten = SystemAPI.QueryBuilder().WithAll<FogMemoryVisual>().WithNone<FogMemory>().Build();
        }

        public void OnDestroy(ref SystemState state) => _visualPrefabs.Dispose();

        public void OnUpdate(ref SystemState state)
        {
            if (_undrawn.IsEmpty && _forgotten.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            var entityManager = state.EntityManager;
            using var forgotten = _forgotten.ToComponentDataArray<FogMemoryVisual>(Allocator.Temp);
            foreach (var visual in forgotten)
            {
                // Gone already when its scene unloaded or its type had no prefab.
                if (entityManager.Exists(visual.Root))
                {
                    entityManager.DestroyEntity(visual.Root);
                }
            }

            entityManager.RemoveComponent<FogMemoryVisual>(_forgotten);
            if (_undrawn.IsEmpty || !SystemAPI.TryGetSingleton(out PrefabRegistry registry))
            {
                return;
            }

            using var memories = _undrawn.ToEntityArray(Allocator.Temp);
            foreach (var memory in memories)
            {
                var record = entityManager.GetComponentData<FogMemory>(memory).State;
                entityManager.AddComponentData(memory, new FogMemoryVisual { Root = Draw(entityManager, registry, record) });
            }
        }

        private Entity Draw(EntityManager entityManager, in PrefabRegistry registry, in SeenState record)
        {
            var prefab = registry.Find(record.TypeId);
            if (prefab == Entity.Null)
            {
                Debug.LogError($"Fog memory: no prefab registered for type id {record.TypeId}, so it isn't drawn.");
                return Entity.Null;
            }

            // Visual prefabs belong to their source's scene section, so they unload with it.
            var built = _visualPrefabs.TryGetValue(prefab, out var visualPrefab) && entityManager.Exists(visualPrefab);
            if (!built)
            {
                visualPrefab = VisualPrefab.Build(entityManager, prefab);
                _visualPrefabs[prefab] = visualPrefab;
            }

            var root = entityManager.Instantiate(visualPrefab);
            entityManager.SetComponentData(root, record.Transform);
            entityManager.AddComponentData(root, new TeamColorSource { Owner = record.Owner });
            return root;
        }
    }
}
