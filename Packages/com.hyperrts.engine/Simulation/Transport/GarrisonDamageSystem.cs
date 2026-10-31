using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Frees the slots of passengers who died inside, then shares each <see cref="ShelterHit"/> evenly among the
    /// living, as hits the damage queue resolves next frame with each passenger's own armor. Runs before
    /// <see cref="ContainerDeathSystem"/>, so passengers blown out by the hit that destroys their shelter still take
    /// their share.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(ContainerDeathSystem))]
    public partial struct GarrisonDamageSystem : ISystem
    {
        private DamageWriter _damage;
        private ComponentLookup<Health> _health;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DamageQueue>();
            state.RequireForUpdate<Container>();
            _damage = new DamageWriter(ref state);
            _health = state.GetComponentLookup<Health>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damage.Update(ref state, SystemAPI.GetSingletonEntity<DamageQueue>());
            _health.Update(ref state);
            new PruneJob { Health = _health }.Schedule();
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
                    if (!Common.Health.IsAlive(Health, cargo[i].Unit))
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
            public DamageWriter Damage;

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
                    share.Amount /= cargo.Length;
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
