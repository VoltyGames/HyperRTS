using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>Drops this frame's <see cref="PlayerCommand"/>s once every consumer in the order phase has run.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderLast = true)]
    public partial struct PlayerCommandClearSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ClearJob().ScheduleParallel();
        }

        [BurstCompile]
        private partial struct ClearJob : IJobEntity
        {
            private void Execute(DynamicBuffer<PlayerCommand> commands) => commands.Clear();
        }
    }
}
