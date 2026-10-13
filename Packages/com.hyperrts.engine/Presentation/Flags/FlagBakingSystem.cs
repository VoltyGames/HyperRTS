using HyperRTS.Simulation.Buildings;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>
    /// Gives each <see cref="FlagMount"/>'s flag the material properties <see cref="FlagSystem"/> fills. A baking system,
    /// not a baker: bakers may only add to their own entities, and Simulation cannot name these components.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct FlagBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var states = SystemAPI.GetComponentLookup<FlagState>(true);
            var commands = new EntityCommandBuffer(Allocator.Temp);
            foreach (var mount in SystemAPI.Query<RefRO<FlagMount>>()
                         .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                // Incremental baking re-runs this on flags that already have them.
                var flag = mount.ValueRO.Flag;
                if (states.HasComponent(flag))
                {
                    continue;
                }

                commands.AddComponent<FlagState>(flag);
                commands.AddComponent<FlagColor>(flag);
            }

            commands.Playback(state.EntityManager);
        }
    }
}
