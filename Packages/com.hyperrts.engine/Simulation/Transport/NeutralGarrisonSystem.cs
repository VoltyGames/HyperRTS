using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Hands a <see cref="NeutralGarrison"/> building to the owner of its passengers, so enemies can fight over it,
    /// and back to neutral once it empties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(ContainerDeathSystem))]
    public partial struct NeutralGarrisonSystem : ISystem
    {
        private ComponentLookup<Faction> _factions;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NeutralGarrison>();
            _factions = state.GetComponentLookup<Faction>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _factions.Update(ref state);
            state.Dependency = new OwnerJob { Factions = _factions }.Schedule(state.Dependency);
        }

        /// <summary>Single-threaded: reads passengers' factions while writing the building's.</summary>
        [BurstCompile]
        [WithAll(typeof(NeutralGarrison))]
        private partial struct OwnerJob : IJobEntity
        {
            // Passengers are never their own building, so reading their faction can't alias the one being written.
            [ReadOnly] [NativeDisableContainerSafetyRestriction] public ComponentLookup<Faction> Factions;

            private void Execute(ref Faction faction, in DynamicBuffer<Cargo> cargo)
            {
                faction.Value = cargo.Length > 0 && Factions.TryGetComponent(cargo[0].Unit, out var passenger)
                    ? passenger.Value
                    : Faction.Neutral;
            }
        }
    }
}
