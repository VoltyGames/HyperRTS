using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests.Transport;

/// <summary>Boarding helpers for transports, bunkers and garrisons on top of <see cref="TestWorld"/>.</summary>
public static class ContainerTestKit
{
    extension(TestWorld world)
    {
        /// <summary>Smart-clicks <paramref name="container"/> for <paramref name="unit"/>'s owner, as boarding does.</summary>
        public void Enter(Entity unit, Entity container) =>
            world.Command(world.Get<Owner>(unit).Value,
                new PlayerCommand { Type = CommandType.Smart, Unit = unit, Target = container });

        public bool IsInside(Entity unit) => world.IsEnabled<Inside>(unit);
    }
}
