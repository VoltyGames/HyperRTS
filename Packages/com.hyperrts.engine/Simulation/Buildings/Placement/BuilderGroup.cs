using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Placement
{
    /// <summary>The builders a placement command goes to, which know where their orders leave them.</summary>
    internal readonly struct BuilderGroup
    {
        private readonly EntityManager _entities;
        private readonly NativeList<Entity> _builders;

        public BuilderGroup(EntityManager entities, NativeList<Entity> builders)
        {
            _entities = entities;
            _builders = builders;
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
                var standing = _entities.GetComponentData<LocalTransform>(builder).Position;
                middle += queued ? QueueEnd(builder) : standing;
            }

            middle /= _builders.Length;
            var first = math.distancesq(middle.xz, orders[0].Position.xz);
            if (math.distancesq(middle.xz, orders[orders.Length - 1].Position.xz) < first)
            {
                orders.ReverseFrom(0);
            }
        }

        /// <summary>Where the builder's orders leave it: its last queued order, its active one or its place.</summary>
        private float3 QueueEnd(Entity builder)
        {
            var queue = _entities.GetBuffer<QueuedOrder>(builder, true);
            if (queue.Length > 0)
            {
                return queue[queue.Length - 1].Value.Position;
            }

            if (_entities.IsComponentEnabled<ActiveOrder>(builder))
            {
                return _entities.GetComponentData<ActiveOrder>(builder).Value.Position;
            }

            return _entities.GetComponentData<LocalTransform>(builder).Position;
        }
    }
}
