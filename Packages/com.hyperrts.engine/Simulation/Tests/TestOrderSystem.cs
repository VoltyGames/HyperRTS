using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Holds an <see cref="OrderWriter"/> so tests give orders the way the simulation does.</summary>
    [DisableAutoCreation]
    public partial struct TestOrderSystem : ISystem
    {
        private OrderWriter _writer;

        public void OnCreate(ref SystemState state) => _writer = new OrderWriter(ref state);

        public void Issue(ref SystemState state, Entity unit, in Order order, bool queue)
        {
            state.EntityManager.CompleteAllTrackedJobs();
            _writer.Update(ref state);
            _writer.Issue(unit, order, queue);
        }
    }
}
