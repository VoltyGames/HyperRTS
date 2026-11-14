using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Deploy
{
    /// <summary>
    /// Keeps a <see cref="Deployable"/> that isn't packed up where it stands: a move asked of it packs it up, and
    /// waits, asked again each frame by its order, until it has.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    [UpdateBefore(typeof(PathfindingSystem))]
    public partial struct DeployHoldSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<Deployable>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new HoldJob().ScheduleParallel();

        [BurstCompile]
        [WithNone(typeof(Dead))]
        private partial struct HoldJob : IJobEntity
        {
            // Combat never moves a unit that isn't packed up, so a move here is an order to go elsewhere.
            private void Execute(ref Deployable deployable, EnabledRefRW<MoveDestination> moving)
            {
                deployable.PackUp();
                moving.ValueRW = !deployable.Immobile;
            }
        }
    }
}
