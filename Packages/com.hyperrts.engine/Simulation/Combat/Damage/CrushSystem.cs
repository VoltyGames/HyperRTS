using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Damage
{
    /// <summary>
    /// Crushers flatten the <see cref="Destructible"/> scenery they drive over (fences, hay, small trees) with a
    /// lethal hit, so remains and sounds follow the normal death path. Crushing credits no one.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(DamageSystem))]
    public partial struct CrushSystem : ISystem
    {
        // Contact slack past the hull, so scenery brushed by a track still goes down.
        private const float Reach = 0.25f;
        private const float Lethal = 1e6f;

        private EventWriter<DamageEvent> _damage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<Crusher>();
            _damage = new EventWriter<DamageEvent>(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state);
            new CrushJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Damage = _damage,
                Destructibles = SystemAPI.GetComponentLookup<Destructible>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
            }.Schedule();
        }

        /// <summary>Single-threaded: hits go into the one damage queue.</summary>
        [BurstCompile]
        [WithNone(typeof(Inside), typeof(Dead))]
        private partial struct CrushJob : IJobEntity
        {
            [ReadOnly] public SpatialIndex Index;
            public EventWriter<DamageEvent> Damage;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(Entity entity, in Crusher crusher, in NavAgent agent, in LocalTransform transform)
            {
                if (agent.Layer == NavLayer.Air || agent.Layer == NavLayer.Naval)
                {
                    return;
                }

                var underfoot = new Underfoot
                {
                    Damage = Damage,
                    Destructibles = Destructibles,
                    Health = Health,
                    Crusher = entity,
                    Size = crusher.Size,
                    Position = transform.Position,
                    Rotation = transform.Rotation,
                    Agent = agent,
                };
                Index.Query(transform.Position, agent.HalfLength + Reach, ref underfoot);
            }
        }

        /// <summary>Hits each crushable victim as the index finds it, so hits queue in the index's stable order.</summary>
        private struct Underfoot : ISpatialVisitor
        {
            public EventWriter<DamageEvent> Damage;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<Health> Health;
            public Entity Crusher;
            public int Size;
            public float3 Position;
            public quaternion Rotation;
            public NavAgent Agent;

            public void Visit(in SpatialEntry entry)
            {
                var victim = entry.Entity;
                var crushable = Destructibles.TryGetComponent(victim, out var destructible) && destructible.Crush > 0;
                var flattened = crushable && destructible.Crush <= Size && Underneath(entry);
                if (!flattened || !Common.Health.IsAlive(Health, victim))
                {
                    return;
                }

                Damage.Add(DamageEvent.Direct(victim, Position, Crusher, 0, Lethal));
            }

            private readonly bool Underneath(in SpatialEntry entry) =>
                Agent.FootprintGap(Position.xz, Rotation, entry.Position.xz) <= entry.Radius + Reach;
        }
    }
}
