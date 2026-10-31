using HyperRTS.Core;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Follows a building's broken mask in every world, whether its own damage broke the sections or the server's
    /// changes did: sections on the ground open and close their <see cref="NavObstaclePart"/>s and deck sections their
    /// <see cref="NavAreaPart"/>s (one per section, in order), and newly broken outer walls leave <see cref="SectionRubble"/>.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct SectionSettleSystem : ISystem
    {
        private ComponentLookup<Sections> _buildings;
        private BufferLookup<NavObstaclePart> _parts;
        private BufferLookup<NavAreaPart> _decks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<Sections>();
            _parts = state.GetBufferLookup<NavObstaclePart>();
            _decks = state.GetBufferLookup<NavAreaPart>();
            state.RequireForUpdate<Sections>();
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
        [WithAll(typeof(Sections))]
        private partial struct SettleJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;
            public ComponentLookup<Sections> Buildings;
            public BufferLookup<NavObstaclePart> Parts;
            public BufferLookup<NavAreaPart> Decks;

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
                    if ((broken & (1ul << i)) != 0 && building.Rubble != Entity.Null && SectionRubble.Leaves(sections[i]))
                    {
                        SectionRubble.Spawn(Ecb, building.Rubble, transform, sections[i]);
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
