using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using HyperRTS.Simulation.Upgrades;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>Starts the first unmet step it can; true once every step is met (or there is no build order).</summary>
        private bool FollowBuildOrder(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            if (!SystemAPI.HasBuffer<AIBuildStep>(turn.Player))
            {
                return true;
            }

            var counts = CountOwned(ref state, turn, snapshot);
            var done = true;
            foreach (var step in SystemAPI.GetBuffer<AIBuildStep>(turn.Player))
            {
                if (!SystemAPI.HasComponent<EntityInfo>(step.Prefab))
                {
                    continue;
                }

                counts.TryGetValue(SystemAPI.GetComponent<EntityInfo>(step.Prefab).TypeId, out var owned);
                var wanted = SystemAPI.HasComponent<Upgrade>(step.Prefab) ? math.min(step.Count, 1) : step.Count;
                if (owned >= wanted)
                {
                    continue;
                }

                done = false;
                if (TryStart(ref state, turn, snapshot, step.Prefab))
                {
                    break;
                }
            }

            return done;
        }

        /// <summary>Per type id: living units and buildings (sites too), queued production and finished research.</summary>
        private NativeHashMap<int, int> CountOwned(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var counts = new NativeHashMap<int, int>(64, Allocator.Temp);
            for (var i = 0; i < snapshot.Owned.Length; i++)
            {
                if (snapshot.Owned.IsOwnedBy(i, turn.Faction))
                {
                    Increment(counts, snapshot.OwnedInfo[i].TypeId);
                }
            }

            foreach (var producer in snapshot.Producers)
            {
                if (SystemAPI.GetComponent<Faction>(producer).Value != turn.Faction)
                {
                    continue;
                }

                foreach (var item in SystemAPI.GetBuffer<ProductionQueueItem>(producer))
                {
                    Increment(counts, item.TypeId);
                }
            }

            foreach (var upgrade in SystemAPI.GetBuffer<ResearchedUpgrade>(turn.Player))
            {
                Increment(counts, upgrade.TypeId);
            }

            return counts;
        }

        private static void Increment(NativeHashMap<int, int> counts, int typeId)
        {
            counts.TryGetValue(typeId, out var count);
            counts[typeId] = count + 1;
        }

        private bool TryStart(ref SystemState state, in Turn turn, in Snapshot snapshot, Entity prefab)
        {
            var stock = SystemAPI.GetBuffer<ResourceStock>(turn.Player);
            if (!ResourceMath.CanAfford(stock, SystemAPI.GetBuffer<ResourceCost>(prefab)))
            {
                return false;
            }

            if (!snapshot.Completed.MeetsPrerequisites(SystemAPI.GetBuffer<Prerequisite>(prefab), turn.Faction))
            {
                return false;
            }

            var placeable = SystemAPI.HasComponent<BuildingTag>(prefab) && SystemAPI.HasComponent<NavObstacle>(prefab);
            return placeable ? TryPlace(ref state, turn, snapshot, prefab) : TryQueue(ref state, turn, snapshot, prefab);
        }

        /// <summary>Places the building near home with the best builder, exactly as a human would.</summary>
        private bool TryPlace(ref SystemState state, in Turn turn, in Snapshot snapshot, Entity prefab)
        {
            var builder = PickBuilder(ref state, turn, snapshot, prefab);
            if (builder == Entity.Null || !SystemAPI.TryGetSingleton<MapSettings>(out var map))
            {
                return false;
            }

            SystemAPI.TryGetSingleton<NavGrid>(out var grid);
            var footprint = SystemAPI.GetComponent<NavObstacle>(prefab).Size;
            var surface = SystemAPI.TryGetComponent<BuildingPlacement>(prefab, out var placement)
                ? placement.Surface
                : PlacementSurface.Land;
            if (!AIPlacement.TryFindSpot(map, grid, turn.Home, footprint, surface, out var spot))
            {
                return false;
            }

            turn.Commands.Add(new PlayerCommand
            {
                Type = CommandType.PlaceBuilding, Unit = builder, Prefab = prefab, Position = spot,
            });
            turn.Busy.Add(builder);
            return true;
        }

        /// <summary>Queues the unit or upgrade at the owned producer offering it with the shortest queue.</summary>
        private bool TryQueue(ref SystemState state, in Turn turn, in Snapshot snapshot, Entity prefab)
        {
            var population = SystemAPI.GetComponent<Population>(turn.Player);
            if (!population.HasRoomFor(SystemAPI.GetComponent<Producible>(prefab).Population))
            {
                return false;
            }

            var best = Entity.Null;
            var bestLength = int.MaxValue;
            foreach (var producer in snapshot.Producers)
            {
                var length = SystemAPI.GetBuffer<ProductionQueueItem>(producer).Length;
                if (!CanQueueAt(ref state, producer, turn.Faction, prefab, length))
                {
                    continue;
                }

                if (length < bestLength || (length == bestLength && producer.Index < best.Index))
                {
                    best = producer;
                    bestLength = length;
                }
            }

            if (best == Entity.Null)
            {
                return false;
            }

            turn.Commands.Add(new PlayerCommand { Type = CommandType.Produce, Unit = best, Prefab = prefab });
            return true;
        }

        private bool CanQueueAt(ref SystemState state, Entity producer, byte faction, Entity prefab, int queued)
        {
            if (SystemAPI.GetComponent<Faction>(producer).Value != faction)
            {
                return false;
            }

            var hasRoom = queued < SystemAPI.GetComponent<Producer>(producer).QueueLimit;
            return hasRoom && ProductionRules.Offers(SystemAPI.GetBuffer<ProductionOption>(producer), prefab);
        }
    }
}
