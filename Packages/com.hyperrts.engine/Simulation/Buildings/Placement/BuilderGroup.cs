using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Placement;

/// <summary>The builders a placement command goes to, which know where their orders leave them.</summary>
internal readonly struct BuilderGroup
{
    private readonly EntityManager _entities;
    private readonly NativeList<Entity> _builders;
    private readonly OrderReader _orders;

    public BuilderGroup(EntityManager entities, NativeList<Entity> builders, in OrderReader orders)
    {
        _entities = entities;
        _builders = builders;
        _orders = orders;
    }

    /// <summary>
    /// Orders the sites from the end nearer the builders: where they stand, or for a queued line where their
    /// queue leaves them, so a wall drawn leg by leg is built in one walk.
    /// </summary>
    public void NearEndFirst(NativeList<Order> orders, bool queued)
    {
        if (orders.Length < 2)
        {
            return;
        }

        var middle = float3.zero;
        foreach (var builder in _builders)
        {
            middle += queued ? QueueEnd(builder) : Position(builder);
        }

        middle /= _builders.Length;
        var first = math.distancesq(middle.xz, orders[0].Position.xz);
        if (math.distancesq(middle.xz, orders[orders.Length - 1].Position.xz) < first)
        {
            orders.ReverseFrom(0);
        }
    }

    /// <summary>Where the builder's orders leave it: its last queued order, its active one or its place.</summary>
    private float3 QueueEnd(Entity builder) =>
        _orders.TryGetLastOrder(builder, out var last) ? last.Position : Position(builder);

    private float3 Position(Entity builder) => _entities.GetComponentData<LocalTransform>(builder).Position;
}
