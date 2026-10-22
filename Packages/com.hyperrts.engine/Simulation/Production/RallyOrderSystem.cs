using HyperRTS.Core;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Production
{
    /// <summary>Issues each new unit's <see cref="RallyOrder"/> through <see cref="OrderWriter"/>.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    [UpdateAfter(typeof(OrderDispatchSystem))]
    [UpdateBefore(typeof(MoveOrderSystem))]
    public partial struct RallyOrderSystem : ISystem
    {
        private OrderWriter _writer;
        private EntityQuery _rallied;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _writer = new OrderWriter(ref state);
            _rallied = SystemAPI.QueryBuilder().WithAll<RallyOrder>().Build();
            state.RequireForUpdate(_rallied);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _writer.Update(ref state);
            foreach (var (rally, unit) in SystemAPI.Query<RefRO<RallyOrder>>().WithEntityAccess())
            {
                _writer.Issue(unit, new Order { Type = OrderType.Move, Position = rally.ValueRO.Position }, false);
            }

            state.EntityManager.RemoveComponent<RallyOrder>(_rallied);
        }
    }
}
