using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// Follows a building's broken mask in every world: opens and closes the navigation parts of broken sections and leaves rubble where outer walls fell.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct SectionSettleSystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SectionedBuilding>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SettleJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Buildings = SystemAPI.GetComponentLookup<SectionedBuilding>(false),
                Parts = SystemAPI.GetBufferLookup<NavObstaclePart>(false),
                Decks = SystemAPI.GetBufferLookup<NavAreaPart>(false),
            }.Schedule();
        }

        /// <summary>Writes through lookups only on a change, so standing buildings keep their change versions.</summary>
        [BurstCompile]
        [WithAll(typeof(SectionedBuilding))]
        private partial struct SettleJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public ComponentLookup<SectionedBuilding> Buildings;
            public BufferLookup<NavObstaclePart> Parts;
            public BufferLookup<NavAreaPart> Decks;

            // Every world places the heap at the same spot, so its id is its pose.
            private void SpawnRubble(Entity prefab, in LocalTransform pose)
            {
                var heap = Ecb.Instantiate(prefab);
                Ecb.SetComponent(heap, pose);
                Ecb.AddComponent(heap, SceneryId.At(pose.Position, pose.Rotation));
            }

            private void Execute(Entity entity, in DynamicBuffer<Section> sections, in LocalTransform transform)
            {
                var building = Buildings[entity];
                if (building.Broken == building.Settled)
                {
                    return;
                }

                var broken = building.Broken & ~building.Settled;
                for (var i = 0; i < sections.Length; i++)
                {
                    if ((broken & (1ul << i)) != 0 && building.Rubble != Entity.Null && sections[i].LeavesRubble)
                    {
                        SpawnRubble(building.Rubble, sections[i].RubblePose(transform));
                    }
                }

                if (Parts.TryGetBuffer(entity, out var parts))
                {
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        part.Closed = building.Blocks(sections, i);
                        parts[i] = part;
                    }
                }

                if (Decks.TryGetBuffer(entity, out var decks))
                {
                    for (var i = 0; i < decks.Length; i++)
                    {
                        var deck = decks[i];
                        deck.Open = sections[i].Deck && (building.Broken & (1ul << i)) == 0;
                        decks[i] = deck;
                    }
                }

                building.Settled = building.Broken;
                Buildings[entity] = building;
            }
        }
    }
}
