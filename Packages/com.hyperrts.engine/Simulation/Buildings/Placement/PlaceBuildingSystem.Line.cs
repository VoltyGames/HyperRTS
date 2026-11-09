using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings.Placement
{
    public partial struct PlaceBuildingSystem
    {
        /// <summary>
        /// Places every free segment from the line's start, stopping at the first the player can't buy. Segments are
        /// line pieces, so they keep the building gap from other buildings but not from each other or other walls.
        /// </summary>
        private void PlaceLine(ref SystemState state, in IssuedCommand request, in Buyer buyer,
            EntityCommandBuffer ecb, NativeList<PlacedFootprint> sites, NativeList<Order> orders)
        {
            var prefab = request.Command.Prefab;
            var map = SystemAPI.GetSingleton<MapSettings>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(prefab).Size;
            var surface = BuildingPlacement.GetSurface(state.EntityManager, prefab);
            var join = (LineStart)request.Command.Argument;
            var line = new PlacementLine(map, request.Command.Position, request.Command.End, footprint, surface, join);
            var stock = SystemAPI.GetBuffer<ResourceStock>(request.Player);
            for (var i = 0; i < line.Count; i++)
            {
                var spot = line.Segment(i).Grounded(grid);
                if (!IsFree(ref state, spot, map, grid, request.Owner, sites, inLine: true))
                {
                    continue;
                }

                if (!PurchaseRules.TryBuy(state.EntityManager, prefab, buyer, stock))
                {
                    break;
                }

                orders.Add(Raise(ref state, request, buyer, ecb, sites, spot, inLine: true));
            }
        }

        /// <summary>
        /// Orders the sites from the end nearer the builders: where they stand, or for a queued line where their
        /// queue leaves them, so a wall drawn leg by leg is built in one walk.
        /// </summary>
        private void NearEndFirst(ref SystemState state, NativeList<Entity> builders, NativeList<Order> orders,
            bool queued)
        {
            if (orders.Length < 2)
            {
                return;
            }

            var middle = float3.zero;
            foreach (var builder in builders)
            {
                var standing = SystemAPI.GetComponent<LocalTransform>(builder).Position;
                middle += queued ? QueueEnd(ref state, builder) : standing;
            }

            middle /= builders.Length;
            var first = math.distancesq(middle.xz, orders[0].Position.xz);
            if (math.distancesq(middle.xz, orders[orders.Length - 1].Position.xz) < first)
            {
                orders.ReverseFrom(0);
            }
        }

        /// <summary>Where the builder's orders leave it: its last queued order, its active one or its place.</summary>
        private float3 QueueEnd(ref SystemState state, Entity builder)
        {
            var queue = SystemAPI.GetBuffer<QueuedOrder>(builder);
            if (queue.Length > 0)
            {
                return queue[queue.Length - 1].Value.Position;
            }

            if (SystemAPI.IsComponentEnabled<ActiveOrder>(builder))
            {
                return SystemAPI.GetComponent<ActiveOrder>(builder).Value.Position;
            }

            return SystemAPI.GetComponent<LocalTransform>(builder).Position;
        }
    }
}
