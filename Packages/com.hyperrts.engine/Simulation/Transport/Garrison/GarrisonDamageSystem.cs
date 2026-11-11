using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport.Garrison
{
    /// <summary>
    /// Frees the slots of passengers who died inside, then shares each <see cref="ShelterHit"/> evenly among the
    /// living. Runs before <see cref="ContainerDeathSystem"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(ContainerDeathSystem))]
    public partial struct GarrisonDamageSystem : ISystem
    {
        private EventWriter<DamageEvent> _damage;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Container>();
            _damage = new EventWriter<DamageEvent>(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state);
            new PruneJob { Health = SystemAPI.GetComponentLookup<Health>(true) }.Schedule();
            new ShareJob { Damage = _damage }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Container))]
        private partial struct PruneJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Health> Health;

            private void Execute(DynamicBuffer<Cargo> cargo)
            {
                for (var i = cargo.Length - 1; i >= 0; i--)
                {
                    if (!Common.Combat.Health.IsAlive(Health, cargo[i].Unit))
                    {
                        cargo.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>Single-threaded: hits go into the one damage queue.</summary>
        [BurstCompile]
        [WithAll(typeof(Shelter))]
        private partial struct ShareJob : IJobEntity
        {
            public EventWriter<DamageEvent> Damage;

            private void Execute(DynamicBuffer<ShelterHit> hits, in DynamicBuffer<Cargo> cargo)
            {
                if (cargo.IsEmpty)
                {
                    hits.Clear();
                    return;
                }

                foreach (var item in hits)
                {
                    var share = item.Hit;
                    share.Profile.Amount /= cargo.Length;
                    foreach (var passenger in cargo)
                    {
                        share.Target = passenger.Unit;
                        Damage.Add(share);
                    }
                }

                hits.Clear();
            }
        }
    }
}
