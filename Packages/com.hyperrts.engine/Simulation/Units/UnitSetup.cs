using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Adds the movement and order components on top of <see cref="GameEntitySetup"/>.</summary>
    public static class UnitSetup
    {
        public static void Add<TSink>(ref TSink sink, float moveSpeed, float radius, NavLayer layer = NavLayer.Ground)
            where TSink : struct, IComponentSink
        {
            sink.Add<UnitTag>();
            sink.Add(new MovementSpeed { Value = moveSpeed });
            sink.Add<MoveDestination>();
            sink.SetEnabled<MoveDestination>(false);
            sink.Add(new NavAgent { Radius = radius, Layer = layer });
            sink.AddBuffer<PathWaypoint>();
            sink.Add<PathState>();
            sink.Add<ActiveOrder>();
            sink.SetEnabled<ActiveOrder>(false);
            sink.AddBuffer<QueuedOrder>();
            sink.Add<MoveOrderState>();
            sink.SetEnabled<MoveOrderState>(false);
        }
    }
}
