using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests.Economy
{
    /// <summary>Boarding helpers for transports, bunkers and garrisons on top of <see cref="TestWorld"/>.</summary>
    public static class ContainerTestKit
    {
        /// <summary>Smart-clicks <paramref name="container"/> for <paramref name="unit"/>'s owner, as boarding does.</summary>
        public static void Enter(this TestWorld world, Entity unit, Entity container) =>
            world.Command(world.Get<Faction>(unit).Value,
                new PlayerCommand { Type = CommandType.Smart, Unit = unit, Target = container });

        public static bool IsInside(this TestWorld world, Entity unit) => world.IsEnabled<Inside>(unit);
    }
}
