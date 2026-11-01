using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
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
                Factions = SystemAPI.GetComponentLookup<Faction>(true),
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
            [ReadOnly] public ComponentLookup<Faction> Factions;
            public EventWriter<OwnerChanged> OwnerChanges;
            public EntityCommandBuffer Ecb;

            private void Execute(Entity entity, in Faction faction, in DynamicBuffer<Cargo> cargo)
            {
                var owner = Owner(cargo);
                if (owner == faction.Value)
                {
                    return;
                }

                OwnerChanged.Transfer(Ecb, ref OwnerChanges, entity, faction.Value, owner);
            }

            /// <summary>The first passenger's faction, or neutral when empty.</summary>
            private byte Owner(in DynamicBuffer<Cargo> cargo)
            {
                if (cargo.Length == 0)
                {
                    return Faction.Neutral;
                }

                return Factions.TryGetComponent(cargo[0].Unit, out var passenger) ? passenger.Value : Faction.Neutral;
            }
        }
    }
}
