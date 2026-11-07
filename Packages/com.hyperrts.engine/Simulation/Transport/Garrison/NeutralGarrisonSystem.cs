using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport.Garrison
{
    /// <summary>
    /// Hands a <see cref="NeutralGarrison"/> building to the owner of its passengers, so enemies can fight over it,
    /// and back to neutral once it empties. The change goes through <see cref="OwnerChanged.Transfer"/>, so the
    /// building also drops its selection when it changes hands.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(ContainerDeathSystem))]
    public partial struct NeutralGarrisonSystem : ISystem
    {
        private EventWriter<OwnerChanged> _ownerChanges;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NeutralGarrison>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _ownerChanges = new EventWriter<OwnerChanged>(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ownerChanges.Update(ref state);
            state.Dependency = new OwnerJob
            {
                Owners = SystemAPI.GetComponentLookup<Owner>(true),
                OwnerChanges = _ownerChanges,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
            }.Schedule(state.Dependency);
        }

        /// <summary>Single-threaded: every change is added to the one event buffer. Dying buildings are left to die.</summary>
        [BurstCompile]
        [WithAll(typeof(NeutralGarrison))]
        [WithNone(typeof(Dead))]
        private partial struct OwnerJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Owner> Owners;
            public EventWriter<OwnerChanged> OwnerChanges;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, in Owner owner, in DynamicBuffer<Cargo> cargo)
            {
                var passengerOwner = PassengerOwner(cargo);
                if (passengerOwner == owner.Value)
                {
                    return;
                }

                OwnerChanged.Transfer(Ecb, ref OwnerChanges, entity, owner.Value, passengerOwner);
            }

            /// <summary>The first passenger's owner, or neutral when empty.</summary>
            private byte PassengerOwner(in DynamicBuffer<Cargo> cargo)
            {
                if (cargo.Length == 0)
                {
                    return Owner.Neutral;
                }

                return Owners.TryGetComponent(cargo[0].Unit, out var passenger) ? passenger.Value : Owner.Neutral;
            }
        }
    }
}
