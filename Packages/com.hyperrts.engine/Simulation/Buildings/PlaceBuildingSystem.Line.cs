using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    public partial struct PlaceBuildingSystem
    {
        /// <summary>
        /// Places every free segment from the drag start, stopping at the first the player can't buy, and orders the
        /// sites from the end nearer the builders.
        /// </summary>
        private void PlaceLine(ref SystemState state, in IssuedCommand request, in Buyer buyer,
            EntityCommandBuffer ecb, NativeList<Site> sites, NativeList<Entity> builders, NativeList<Order> orders)
        {
            var prefab = request.Command.Prefab;
            var map = SystemAPI.GetSingleton<MapSettings>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(prefab).Size;
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var surface = BuildingPlacement.GetSurface(state.EntityManager, prefab);
            var line = new PlacementLine(map, request.Command.Position, request.Command.End, footprint);
            var stock = SystemAPI.GetBuffer<ResourceStock>(request.Player);
            for (var i = 0; i < line.Count; i++)
            {
                var valid = PlacementMath.Resolve(map, grid, line.Segment(i), line.Footprint, surface, out var center);
                if (!valid || IsOccupied(center, line.Footprint, sites))
                {
                    continue;
                }

                if (!PurchaseRules.TryBuy(state.EntityManager, prefab, buyer, stock))
                {
                    break;
                }

                orders.Add(Raise(ref state, request, buyer, ecb, sites, center, line.Footprint, line.Turn));
            }

            if (IsFarEndNearer(ref state, builders, orders))
            {
                Reverse(orders);
            }
        }

        /// <summary>Whether the builders' centre is nearer the last site than the first.</summary>
        private bool IsFarEndNearer(ref SystemState state, NativeList<Entity> builders, NativeList<Order> orders)
        {
            if (orders.Length < 2)
            {
                return false;
            }

            var middle = float3.zero;
            foreach (var builder in builders)
            {
                middle += SystemAPI.GetComponent<LocalTransform>(builder).Position;
            }

            middle /= builders.Length;
            var first = math.distancesq(middle.xz, orders[0].Position.xz);
            return math.distancesq(middle.xz, orders[orders.Length - 1].Position.xz) < first;
        }

        private static void Reverse(NativeList<Order> orders)
        {
            for (int i = 0, j = orders.Length - 1; i < j; i++, j--)
            {
                (orders[i], orders[j]) = (orders[j], orders[i]);
            }
        }
    }
}
