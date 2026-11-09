using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Prefabs
{
    /// <summary>
    /// Owns the <see cref="PrefabRegistry"/> and rebuilds it only when prefabs load or unload. Runs in initialization so
    /// it stays current while replay playback pauses the gameplay phases.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct PrefabRegistrySystem : ISystem
    {
        private NativeHashMap<int, Entity> _byTypeId;
        private EntityQuery _prefabs;
        private ChangeTracker _registered;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _prefabs = SystemAPI.QueryBuilder().WithAll<EntityInfo, Prefab>()
                .WithOptions(EntityQueryOptions.IncludePrefab).Build();
            _byTypeId = new NativeHashMap<int, Entity>(64, Allocator.Persistent);
            state.EntityManager.CreateSingleton(new PrefabRegistry { ByTypeId = _byTypeId });
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _byTypeId.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Prefab's order version moves only when prefabs load, unload or change shape, not as units spawn and die.
            if (!_registered.OrderChanged<Prefab>(ref state))
            {
                return;
            }

            _byTypeId.Clear();
            var entities = _prefabs.ToEntityArray(Allocator.Temp);
            var infos = _prefabs.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                _byTypeId[infos[i].TypeId] = entities[i];
            }
        }
    }
}
