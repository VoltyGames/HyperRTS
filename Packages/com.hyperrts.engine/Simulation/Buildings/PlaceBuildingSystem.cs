using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Consumes <see cref="CommandType.PlaceBuilding"/> and <see cref="CommandType.PlaceLine"/>: validates each spot,
    /// charges the cost, drops construction sites and gives the commanded builders Build orders on them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct PlaceBuildingSystem : ISystem
    {
        private struct Assignment
        {
            public Entity Builder;
            public Order Order;
            public bool Queue;
        }

        private CommandReader _commands;
        private EntityQuery _obstacles;
        private EntityQuery _completed;
        private EntityQuery _queues;
        private EntityQuery _limited;
        private OrderWriter _orders;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Builder, BuildOption, Faction, Selected>()
                .WithNone<Dead>().Build();
            var types = TypeSet.Single(CommandType.PlaceBuilding).With(CommandType.PlaceLine);
            _commands = new CommandReader(ref state, types, selected);
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _queues = ResearchRules.QueueQuery(Allocator.Temp).Build(ref state);
            _limited = BuildCounts.Query(Allocator.Temp).Build(ref state);
            _orders = new OrderWriter(ref state);
            state.RequireForUpdate<MapSettings>();
            state.RequireForUpdate<NavGrid>();
            state.RequireForUpdate<SpatialIndex>();
            state.RequireForUpdate<FactionRelations>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var requests = _commands.Read(ref state);
            if (requests.Length > 0)
            {
                PlaceAll(ref state, requests);
            }
        }

        private void PlaceAll(ref SystemState state, NativeList<IssuedCommand> requests)
        {
            // Played back right away so the sites exist before builders read the Build orders below.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            // Every standing obstacle's box: the nav grid may lag a frame behind newly placed sites.
            var sites = NavObstacle.ReadBoxes(_obstacles);
            var assignments = new NativeList<Assignment>(Allocator.Temp);
            // One buyer per player for the frame: each site placed counts against the next request's build limits.
            var buyers = new NativeHashMap<byte, Buyer>(4, Allocator.Temp);
            var buildings = new CompletedBuildings(_completed, Allocator.Temp);

            foreach (var request in requests)
            {
                var builders = Builders(ref state, request);
                if (builders.Length == 0)
                {
                    continue;
                }

                if (!buyers.TryGetValue(request.Faction, out var buyer))
                {
                    buyer = Buyer.Snapshot(state.EntityManager, _queues, _limited, request.Faction, buildings,
                        SystemAPI.GetBuffer<CompletedResearch>(request.Player));
                    buyers.Add(request.Faction, buyer);
                }

                var orders = new NativeList<Order>(Allocator.Temp);
                if (request.Command.Type == CommandType.PlaceLine)
                {
                    PlaceLine(ref state, request, buyer, ecb, sites, builders, orders);
                }
                else if (TryPlace(ref state, request, buyer, ecb, sites, out var order))
                {
                    orders.Add(order);
                }

                Assign(assignments, builders, orders, request.Command.Queue);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _orders.Update(ref state, SystemAPI.GetSingleton<OrderTraitTable>());
            foreach (var assignment in assignments)
            {
                _orders.Issue(assignment.Builder, assignment.Order, assignment.Queue);
            }
        }

        /// <summary>The first order follows the command's queue flag; the rest queue behind it.</summary>
        private static void Assign(NativeList<Assignment> assignments, NativeList<Entity> builders,
            NativeList<Order> orders, bool queue)
        {
            foreach (var builder in builders)
            {
                for (var i = 0; i < orders.Length; i++)
                {
                    assignments.Add(new Assignment { Builder = builder, Order = orders[i], Queue = queue || i > 0 });
                }
            }
        }

        private bool TryPlace(ref SystemState state, in IssuedCommand request, in Buyer buyer, EntityCommandBuffer ecb,
            NativeList<NavBox> sites, out Order order)
        {
            order = default;
            var prefab = request.Command.Prefab;
            var map = SystemAPI.GetSingleton<MapSettings>();
            var grid = SystemAPI.GetSingleton<NavGrid>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(prefab).Size;
            var surface = BuildingPlacement.GetSurface(state.EntityManager, prefab);
            var asked = new BuildingSpot(request.Command.Position, request.Command.Yaw, footprint, surface);
            var spot = asked.Snap(map).Grounded(grid);
            if (!IsFree(ref state, spot, map, grid, request.Faction, sites))
            {
                return false;
            }

            var stock = SystemAPI.GetBuffer<ResourceStock>(request.Player);
            if (!PurchaseRules.TryBuy(state.EntityManager, prefab, buyer, stock))
            {
                return false;
            }

            order = Raise(ref state, request, buyer, ecb, sites, spot);
            return true;
        }

        /// <summary>Counts the bought site against the buyer, spawns it and returns the Build order on it.</summary>
        private Order Raise(ref SystemState state, in IssuedCommand request, in Buyer buyer, EntityCommandBuffer ecb,
            NativeList<NavBox> sites, in BuildingSpot spot)
        {
            var prefab = request.Command.Prefab;
            buyer.AddPurchase(state.EntityManager, prefab);
            var building = SpawnSite(ref state, prefab, spot.Center, spot.Rotation, request.Faction, ecb);
            sites.Add(spot.Box);
            return new Order { Type = OrderType.Build, Target = building, Position = spot.Center };
        }

        /// <summary>An unbuilt instance of <paramref name="prefab"/>; builders raise it through Build orders.</summary>
        private Entity SpawnSite(ref SystemState state, Entity prefab, float3 center, quaternion rotation, byte faction,
            EntityCommandBuffer ecb)
        {
            var transform = SystemAPI.GetComponent<LocalTransform>(prefab);
            var building = ecb.Spawn(prefab, transform, SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
                center, math.mul(rotation, transform.Rotation), faction);
            ecb.SetComponent(building, new ConstructionProgress { Value = 0f });
            ecb.SetComponentEnabled<ConstructionProgress>(building, true);
            // Offline from the first frame, before OfflineSystem sees the site.
            ecb.SetComponent(building, new Offline { Reasons = OfflineReason.UnderConstruction });
            ecb.SetComponentEnabled<Offline>(building, true);
            return building;
        }

        /// <summary>The commanded unit, or the player's selected builders, that can place the prefab.</summary>
        private NativeList<Entity> Builders(ref SystemState state, in IssuedCommand request)
        {
            var result = new NativeList<Entity>(Allocator.Temp);
            var prefab = request.Command.Prefab;
            if (!IsPlaceable(ref state, prefab))
            {
                return result;
            }

            var isLine = request.Command.Type == CommandType.PlaceLine;
            if (isLine && !SystemAPI.HasComponent<LinePlacement>(prefab))
            {
                return result;
            }

            foreach (var unit in _commands.Subjects(ref state, request))
            {
                if (CanBuild(ref state, unit, prefab))
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        /// <summary>A building prefab carrying everything placement reads.</summary>
        private bool IsPlaceable(ref SystemState state, Entity prefab)
        {
            if (!SystemAPI.HasComponent<BuildingTag>(prefab) || !SystemAPI.HasComponent<ConstructionProgress>(prefab))
            {
                return false;
            }

            return SystemAPI.HasComponent<NavObstacle>(prefab) && SystemAPI.HasComponent<LocalTransform>(prefab);
        }

        /// <summary>A builder listing <paramref name="prefab"/> among its build options.</summary>
        private bool CanBuild(ref SystemState state, Entity unit, Entity prefab) =>
            SystemAPI.HasBuffer<BuildOption>(unit)
            && ProductionRules.HasOption(SystemAPI.GetBuffer<BuildOption>(unit), prefab);

        /// <summary>
        /// Valid ground, clear of every site by the map's building gap, with no enemy unit on it; a hidden enemy fails
        /// it like any bad spot.
        /// </summary>
        private bool IsFree(ref SystemState state, in BuildingSpot spot, in MapSettings map, in NavGrid grid,
            byte faction, NativeList<NavBox> sites)
        {
            if (!spot.CanPlace(map, grid, sites.AsArray()))
            {
                return false;
            }

            var relations = SystemAPI.GetSingleton<FactionRelations>();
            return !spot.IsBlockedByEnemies(SystemAPI.GetSingleton<SpatialIndex>(), relations, faction);
        }
    }
}
