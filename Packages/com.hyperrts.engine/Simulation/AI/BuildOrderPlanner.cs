using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Works through the player's <see cref="AIBuildStep"/>s: places buildings and queues units and research.</summary>
    internal struct BuildOrderPlanner
    {
        private EntityQuery _obstacles;
        private BuilderPlanner _builders;

        public static BuildOrderPlanner Create(ref SystemState state) => new()
        {
            _obstacles = new EntityQueryBuilder(Allocator.Temp).WithAll<NavObstacle, LocalTransform>()
                .WithNone<Dead>().Build(ref state),
        };

        public void Update(in BuilderPlanner builders)
        {
            _builders = builders;
        }

        /// <summary>
        /// Starts the first unmet step it can; true once every step is met or capped by its build limit (or there is
        /// no build order).
        /// </summary>
        public readonly bool Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            if (!entities.HasBuffer<AIBuildStep>(turn.Player))
            {
                return true;
            }

            var counts = CountOwned(ref state, turn, snapshot);
            var done = true;
            foreach (var step in entities.GetBuffer<AIBuildStep>(turn.Player))
            {
                if (!entities.HasComponent<EntityInfo>(step.Prefab))
                {
                    continue;
                }

                // Walls aren't planned, and field works are only dug in at defence points.
                if (entities.HasComponent<LinePlacement>(step.Prefab) || entities.HasComponent<AIFieldWork>(step.Prefab))
                {
                    continue;
                }

                counts.TryGetValue(entities.GetComponentData<EntityInfo>(step.Prefab).TypeId, out var owned);
                var wanted = entities.HasComponent<Research>(step.Prefab) ? math.min(step.Count, 1) : step.Count;
                if (owned >= wanted)
                {
                    continue;
                }

                // A build limit never lifts by waiting, unlike cost or prerequisites, so the step counts as met.
                if (turn.Buyer.Counts.AtLimit(entities, step.Prefab))
                {
                    continue;
                }

                done = false;
                if (TryStart(ref state, turn, snapshot, step.Prefab))
                {
                    turn.Buyer.AddPurchase(entities, step.Prefab);
                    break;
                }
            }

            return done;
        }

        /// <summary>Per type id: living units and buildings (sites too), queued production and finished research.</summary>
        private static NativeHashMap<int, int> CountOwned(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            var counts = new NativeHashMap<int, int>(64, Allocator.Temp);
            for (var i = 0; i < snapshot.Owned.Length; i++)
            {
                if (snapshot.Owned.IsOwnedBy(i, turn.Owner))
                {
                    Increment(counts, snapshot.OwnedInfo[i].TypeId);
                }
            }

            foreach (var producer in snapshot.Producers)
            {
                if (entities.GetComponentData<Owner>(producer).Value != turn.Owner)
                {
                    continue;
                }

                foreach (var item in entities.GetBuffer<ProductionQueueItem>(producer))
                {
                    Increment(counts, item.TypeId);
                }
            }

            foreach (var research in entities.GetBuffer<CompletedResearch>(turn.Player))
            {
                Increment(counts, research.TypeId);
            }

            return counts;
        }

        private static void Increment(NativeHashMap<int, int> counts, int typeId)
        {
            counts.TryGetValue(typeId, out var count);
            counts[typeId] = count + 1;
        }

        private readonly bool TryStart(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity prefab)
        {
            var entities = state.EntityManager;
            var stock = entities.GetBuffer<ResourceStock>(turn.Player);
            if (!turn.Buyer.CanBuy(entities, prefab, new Stockpile(stock)))
            {
                return false;
            }

            var placeable = entities.HasComponent<BuildingTag>(prefab) && entities.HasComponent<NavObstacle>(prefab);
            return placeable ? TryPlace(ref state, turn, snapshot, prefab) : TryQueue(ref state, turn, snapshot, prefab);
        }

        /// <summary>Places the building near home with the best builder, exactly as a human would.</summary>
        private readonly bool TryPlace(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity prefab)
        {
            var builder = _builders.PickBuilder(ref state, turn, prefab);
            if (builder == Entity.Null)
            {
                return false;
            }

            var entities = state.EntityManager;
            var footprint = entities.GetComponentData<NavObstacle>(prefab).Size;
            var surface = BuildingPlacement.GetSurface(entities, prefab);
            var gap = entities.GetComponentData<AIPlayer>(turn.Player).BuildingGap;
            var sites = PlacedFootprint.Read(entities, _obstacles);
            var found = AIPlacement.TryFindSpot(snapshot.Map, snapshot.Grid, sites.AsArray(),
                new BuildingSpot(turn.Home, 0f, footprint, surface), gap, out var spot);
            if (!found)
            {
                return false;
            }

            turn.Commands.Add(new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = spot.Center,
                Yaw = spot.Yaw,
            });
            turn.Busy.Add(builder);
            return true;
        }

        /// <summary>Queues the unit or upgrade at the owned producer offering it with the shortest queue.</summary>
        private static bool TryQueue(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity prefab)
        {
            var entities = state.EntityManager;
            var population = entities.GetComponentData<Population>(turn.Player);
            if (!population.HasRoomFor(entities.GetComponentData<Producible>(prefab).Population))
            {
                return false;
            }

            var shortest = Closest.None;
            for (var i = 0; i < snapshot.Producers.Length; i++)
            {
                var producer = snapshot.Producers[i];
                var length = entities.GetBuffer<ProductionQueueItem>(producer).Length;
                if (Producer.CanQueue(entities, producer, turn.Owner, prefab))
                {
                    shortest.Consider(i, producer, length);
                }
            }

            if (!shortest.Found)
            {
                return false;
            }

            turn.Commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = shortest.Entity, Prefab = prefab });
            return true;
        }
    }
}
