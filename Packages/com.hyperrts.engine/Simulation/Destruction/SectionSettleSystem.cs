using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// Follows a building's broken mask in every world, whether its own damage broke the sections or the server's
    /// changes did: sections on the ground open and close their <see cref="NavObstaclePart"/>s and deck sections their
    /// <see cref="NavAreaPart"/>s (one per section, in order), and newly broken outer walls leave a heap of rubble, map scenery with an id of its own that multiplayer can crush or clear.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct SectionSettleSystem : ISystem
    {
        private ComponentLookup<SectionedBuilding> _buildings;
        private BufferLookup<NavObstaclePart> _parts;
        private BufferLookup<NavAreaPart> _decks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<SectionedBuilding>();
            _parts = state.GetBufferLookup<NavObstaclePart>();
            _decks = state.GetBufferLookup<NavAreaPart>();
            state.RequireForUpdate<SectionedBuilding>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _buildings.Update(ref state);
            _parts.Update(ref state);
            _decks.Update(ref state);
            new SettleJob
            {
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
                Buildings = _buildings,
                Parts = _parts,
                Decks = _decks,
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
