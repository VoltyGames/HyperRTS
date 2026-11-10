using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Movement.Crushing
{
    /// <summary>
    /// Crushers flatten the <see cref="Destructible"/> scenery they drive over (fences, hay, small trees) with a
    /// lethal hit, so remains and sounds follow the normal death path. Scenery credits no one. A crusher past
    /// <see cref="Crusher.Pace"/> also kills the enemy <see cref="Crushable"/> units under its hull, each squad
    /// member on its own, crediting the crusher's owner.
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
            state.RequireForUpdate<OwnerRelations>();
            state.RequireForUpdate<SquadRules>();
            _damage = new EventWriter<DamageEvent>(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state);
            var squad = SystemAPI.GetSingleton<SquadRules>();
            new CrushJob
            {
                Index = SystemAPI.GetSingleton<SpatialIndex>(),
                Damage = _damage,
                Relations = SystemAPI.GetSingleton<OwnerRelations>(),
                Spread = math.max(squad.MaxSpread, squad.WorkSpread),
                Destructibles = SystemAPI.GetComponentLookup<Destructible>(true),
                Crushables = SystemAPI.GetComponentLookup<Crushable>(true),
                Members = SystemAPI.GetBufferLookup<SquadMember>(true),
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
            public OwnerRelations Relations;
            public float Spread;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<Crushable> Crushables;
            [ReadOnly] public BufferLookup<SquadMember> Members;
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(Entity entity, in Crusher crusher, in NavAgent agent, in LocalTransform transform,
                in Owner owner, in Velocity velocity)
            {
                if (agent.Layer == NavLayer.Air || agent.Layer == NavLayer.Naval)
                {
                    return;
                }

                var underfoot = new Underfoot
                {
                    Damage = Damage,
                    Relations = Relations,
                    Destructibles = Destructibles,
                    Crushables = Crushables,
                    Members = Members,
                    Health = Health,
                    Crusher = entity,
                    Owner = owner.Value,
                    Size = crusher.Size,
                    CrushesUnits = math.lengthsq(velocity.Value) > Crusher.Pace * Crusher.Pace,
                    Position = transform.Position,
                    Rotation = transform.Rotation,
                    Agent = agent,
                };
                Index.Query(transform.Position, agent.HalfLength + Reach + Spread, ref underfoot);
            }
        }

        /// <summary>Hits each crushable victim as the index finds it, so hits queue in the index's stable order.</summary>
        private struct Underfoot : ISpatialVisitor
        {
            public EventWriter<DamageEvent> Damage;
            public OwnerRelations Relations;
            [ReadOnly] public ComponentLookup<Destructible> Destructibles;
            [ReadOnly] public ComponentLookup<Crushable> Crushables;
            [ReadOnly] public BufferLookup<SquadMember> Members;
            [ReadOnly] public ComponentLookup<Health> Health;
            public Entity Crusher;
            public byte Owner;
            public int Size;
            public bool CrushesUnits;
            public float3 Position;
            public quaternion Rotation;
            public NavAgent Agent;

            public void Visit(in SpatialEntry entry)
            {
                var victim = entry.Entity;
                if (!Common.Health.IsAlive(Health, victim))
                {
                    return;
                }

                if (Destructibles.TryGetComponent(victim, out var destructible))
                {
                    FlattenScenery(entry, destructible.Crush);
                }
                else if (CrushesUnits && Crushables.TryGetComponent(victim, out var crushable))
                {
                    CrushUnit(entry, crushable.Crush);
                }
            }

            private void FlattenScenery(in SpatialEntry entry, int crush)
            {
                if (crush > 0 && crush <= Size && Underneath(entry.Position, entry.Radius))
                {
                    Damage.Add(DamageEvent.Direct(entry.Entity, Position, Crusher, 0, Lethal));
                }
            }

            private void CrushUnit(in SpatialEntry entry, int crush)
            {
                if (crush <= 0 || crush > Size || Relations.IsAllied(Owner, entry.Owner))
                {
                    return;
                }

                if (!Members.TryGetBuffer(entry.Entity, out var members))
                {
                    if (Underneath(entry.Position, entry.Radius))
                    {
                        Damage.Add(DamageEvent.Direct(entry.Entity, Position, Crusher, Owner, Lethal));
                    }

                    return;
                }

                for (var i = 0; i < members.Length; i++)
                {
                    var place = members[i].Position(entry.Position);
                    if (members[i].Alive && Underneath(place, SquadMember.Radius))
                    {
                        Damage.Add(DamageEvent.Direct(entry.Entity, place, Crusher, Owner, Lethal).From(Position).Aimed(i));
                    }
                }
            }

            private readonly bool Underneath(float3 place, float radius) =>
                Agent.FootprintGap(Position.xz, Rotation, place.xz) <= radius + Reach;
        }
    }
}
