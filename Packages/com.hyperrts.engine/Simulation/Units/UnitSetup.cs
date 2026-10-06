using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;

namespace HyperRTS.Simulation.Units
{
    /// <summary>Adds the movement and order components on top of <c>GameEntitySetup</c>.</summary>
    public static class UnitSetup
    {
        public static void Add<TWriter>(ref TWriter writer, float moveSpeed, float radius,
            NavLayer layer = NavLayer.Ground) where TWriter : struct, IEntityWriter
        {
            writer.Add<UnitTag>();
            writer.Add(new MovementSpeed { Value = moveSpeed });
            writer.Add<MoveDestination>();
            writer.SetEnabled<MoveDestination>(false);
            writer.Add(new NavAgent { Radius = radius, Layer = layer });
            writer.AddBuffer<PathWaypoint>();
            writer.Add<PathState>();
            writer.Add<ActiveOrder>();
            writer.SetEnabled<ActiveOrder>(false);
            writer.AddBuffer<QueuedOrder>();
            writer.Add<MoveOrderState>();
            writer.SetEnabled<MoveOrderState>(false);
        }
    }
}
