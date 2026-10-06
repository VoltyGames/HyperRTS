using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
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
    /// Consumes <see cref="CommandType.PlaceBuilding"/>: validates the spot, charges the cost, drops a construction
    /// site and gives the commanded builders a Build order on it.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct PlaceBuildingSystem : ISystem
    {
        private struct Request
        {
            public Entity Player;
            public byte Faction;
            public PlayerCommand Command;
        }

        private struct Assignment
        {
            public Entity Builder;
            public Order Order;
            public bool Queue;
        }

        private struct Site
        {
            public float3 Center;
            public float2 Footprint;
        }

        private EntityQuery _selectedBuilders;
        private EntityQuery _obstacles;
        private EntityQuery _completed;
        private OrderWriter _orders;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selectedBuilders = SystemAPI.QueryBuilder().WithAll<Builder, BuildOption, Faction, Selected>()
                .WithNone<Dead>().Build();
            _obstacles = SystemAPI.QueryBuilder().WithAll<NavObstacle, LocalTransform>().WithNone<Dead>().Build();
            _completed = CompletedBuildings.Query(Allocator.Temp).Build(ref state);
            _orders = new OrderWriter(ref state);
            state.RequireForUpdate<MapSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var requests = new NativeList<Request>(Allocator.Temp);
            foreach (var (player, commands, entity) in SystemAPI.Query<RefRO<Player>, DynamicBuffer<PlayerCommand>>()
                         .WithNone<Defeated>().WithEntityAccess())
            {
                foreach (var command in commands)
                {
                    if (command.Type == CommandType.PlaceBuilding)
                    {
                        requests.Add(new Request { Player = entity, Faction = player.ValueRO.Faction, Command = command });
                    }
                }
            }

            if (requests.Length > 0)
            {
                PlaceAll(ref state, requests);
            }
        }

        private void PlaceAll(ref SystemState state, NativeList<Request> requests)
        {
            // Played back right away so the Build orders below can target the real site entities.
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var sites = new NativeList<Site>(Allocator.Temp);
            var assignments = new NativeList<Assignment>(Allocator.Temp);

            foreach (var request in requests)
            {
                var builders = Builders(ref state, request);
                if (builders.Length == 0 || !TryPlace(ref state, request, ecb, sites, out var order))
                {
                    continue;
                }

                foreach (var builder in builders)
                {
                    assignments.Add(new Assignment { Builder = builder, Order = order, Queue = request.Command.Queue });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _orders.Update(ref state);
            foreach (var assignment in assignments)
            {
                _orders.Issue(assignment.Builder, assignment.Order, assignment.Queue);
            }
        }

        private bool TryPlace(ref SystemState state, in Request request, EntityCommandBuffer ecb,
            NativeList<Site> sites, out Order order)
        {
            order = default;
            var prefab = request.Command.Prefab;
            var map = SystemAPI.GetSingleton<MapSettings>();
            var footprint = SystemAPI.GetComponent<NavObstacle>(prefab).Size;
            var surface = BuildingPlacement.SurfaceOf(state.EntityManager, prefab);
            SystemAPI.TryGetSingleton<NavGrid>(out var grid);
            if (!PlacementMath.Resolve(map, grid, request.Command.Position, footprint, surface, out var center))
            {
                return false;
            }

            if (IsOccupied(center, footprint, sites))
            {
                return false;
            }

            var required = SystemAPI.GetBuffer<Prerequisite>(prefab);
            if (!CompletedBuildings.MeetsPrerequisites(required, request.Faction, _completed))
            {
                return false;
            }

            var stock = SystemAPI.GetBuffer<ResourceStock>(request.Player);
            if (!ResourceMath.TrySpend(stock, SystemAPI.GetBuffer<ResourceCost>(prefab)))
            {
                return false;
            }

            var building = ecb.Instantiate(prefab);
            var transform = SystemAPI.GetComponent<LocalTransform>(prefab);
            transform.Position = center;
            ecb.SetComponent(building, transform);
            ecb.SetComponent(building, new Faction { Value = request.Faction });
            ecb.SetComponent(building, new ConstructionProgress { Value = 0f });
            ecb.SetComponentEnabled<ConstructionProgress>(building, true);

            sites.Add(new Site { Center = center, Footprint = footprint });
            order = new Order { Type = OrderType.Build, Target = building, Position = center };
            return true;
        }

        /// <summary>The commanded unit, or the player's selected builders, that can place the prefab.</summary>
        private NativeList<Entity> Builders(ref SystemState state, in Request request)
        {
            var result = new NativeList<Entity>(Allocator.Temp);
            var prefab = request.Command.Prefab;
            if (!SystemAPI.HasComponent<BuildingTag>(prefab) || !SystemAPI.HasComponent<ConstructionProgress>(prefab) ||
                !SystemAPI.HasComponent<NavObstacle>(prefab) || !SystemAPI.HasComponent<LocalTransform>(prefab))
            {
                return result;
            }

            var listed = SystemAPI.GetBuffer<PlayerCommandSubject>(request.Player);
            foreach (var unit in CommandSubjects.Collect(request.Command, listed, _selectedBuilders))
            {
                if (SystemAPI.HasBuffer<BuildOption>(unit) && SystemAPI.HasComponent<Faction>(unit) &&
                    SystemAPI.GetComponent<Faction>(unit).Value == request.Faction &&
                    ProductionRules.Offers(SystemAPI.GetBuffer<BuildOption>(unit), prefab))
                {
                    result.Add(unit);
                }
            }

            return result;
        }

        /// <summary>Checks existing obstacles too: the nav grid may lag a frame behind newly placed sites.</summary>
        private bool IsOccupied(float3 center, float2 footprint, NativeList<Site> sites)
        {
            foreach (var site in sites)
            {
                if (Overlaps(center, footprint, site.Center, site.Footprint))
                {
                    return true;
                }
            }

            var obstacles = _obstacles.ToComponentDataArray<NavObstacle>(Allocator.Temp);
            var transforms = _obstacles.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            for (var i = 0; i < obstacles.Length; i++)
            {
                if (Overlaps(center, footprint, transforms[i].Position, obstacles[i].Size))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Touching edges are allowed so buildings can sit flush.</summary>
        private static bool Overlaps(float3 a, float2 sizeA, float3 b, float2 sizeB) =>
            math.all(math.abs(a.xz - b.xz) * 2f < sizeA + sizeB - 0.001f);
    }
}
