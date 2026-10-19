using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>
    /// Play-mode load test: two armies spawned at opposite bases that attack-move at each other, and what the
    /// connections carry while they do. Editor only.
    /// </summary>
    internal static class NetworkStress
    {
        private const float Spacing = 3.5f;
        private const float FrontDistance = 25f;

        /// <summary>One reading of the presented world's connection and ghost freshness.</summary>
        public struct Sample
        {
            public float ReceivedKbps;
            public float ReceivedMeanKbps;
            public float ReceivedPeakKbps;
            public ulong ReceivedBytes;
            public float AveragePacketBytes;
            public int Units;
            public float MeanUnitAgeTicks;
            public int MaxUnitAgeTicks;
            public int TickRate;

            public override string ToString() =>
                $"rx {ReceivedKbps:F0} kbps (mean {ReceivedMeanKbps:F0}, peak {ReceivedPeakKbps:F0}), " +
                $"{ReceivedBytes / 1024f:F0} KiB total, {AveragePacketBytes:F0} B/packet, " +
                $"{Units} units, update age mean {MeanUnitAgeTicks:F1} / max {MaxUnitAgeTicks} ticks at {TickRate} Hz";
        }

        /// <summary>
        /// Spawns <paramref name="perSide"/> of <paramref name="prefab"/> in front of the first player's base and of
        /// the farthest hostile base, each army attack-moving to the other base. Returns the units spawned.
        /// </summary>
        public static int SpawnArmies(EntityManager entityManager, Entity prefab, int perSide)
        {
            if (!TryFindOpposingBases(entityManager, out var first, out var second))
            {
                Debug.LogWarning("[HyperRTS] The armies cheat needs two hostile factions with buildings.");
                return 0;
            }

            PlayWorld.TryGetSingleton(entityManager, out TerrainHeight terrain);
            return Army(entityManager, prefab, perSide, first, second, terrain) +
                   Army(entityManager, prefab, perSide, second, first, terrain);
        }

        /// <summary>Reads the presented (client) world: transport receive rates and how old each unit's latest snapshot is.</summary>
        public static bool TryRead(out Sample sample)
        {
            sample = default;
            if (!PlayWorld.TryGet(WorldSystemFilterFlags.ClientSimulation, out var entityManager))
            {
                return false;
            }

            if (!PlayWorld.TryGetSingleton(entityManager, out NetworkStreamDriver driver) ||
                !PlayWorld.TryGetSingleton(entityManager, out NetworkTime time) ||
                !PlayWorld.TryGetSingleton(entityManager, out ClientServerTickRate tickRate))
            {
                return false;
            }

            ReadTransport(driver.DriverStore, ref sample);
            ReadUnitAges(entityManager, time.ServerTick, ref sample);
            sample.TickRate = tickRate.NetworkTickRate;
            return true;
        }

        private static void ReadTransport(NetworkDriverStore store, ref Sample sample)
        {
            ulong packets = 0;
            for (var id = store.FirstDriver; id < store.LastDriver; id++)
            {
                var stats = store.GetNetworkDriver(id).GetStatistics();
                sample.ReceivedKbps += stats.RxBandwidth.Current;
                sample.ReceivedMeanKbps += stats.RxBandwidth.Mean;
                sample.ReceivedPeakKbps += stats.RxBandwidth.Maximum;
                sample.ReceivedBytes += stats.RxTotalBytes;
                packets += stats.RxTotalPackets;
            }

            sample.AveragePacketBytes = packets > 0 ? (float)sample.ReceivedBytes / packets : 0f;
        }

        // A snapshot slot starts with its server tick; the latest slot says when the ghost last changed on the wire.
        private static void ReadUnitAges(EntityManager entityManager, NetworkTick now, ref Sample sample)
        {
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<UnitTag, SnapshotData, SnapshotDataBuffer>().Build(entityManager);
            using var units = query.ToEntityArray(Allocator.Temp);
            long total = 0;
            foreach (var unit in units)
            {
                var data = entityManager.GetComponentData<SnapshotData>(unit);
                var buffer = entityManager.GetBuffer<SnapshotDataBuffer>(unit, true);
                var offset = data.LatestIndex * data.SnapshotSize;
                if (buffer.Length < offset + 4)
                {
                    continue;
                }

                var tick = new NetworkTick
                {
                    SerializedData = (uint)(buffer[offset].Value | buffer[offset + 1].Value << 8 |
                                            buffer[offset + 2].Value << 16 | buffer[offset + 3].Value << 24),
                };
                var age = tick.IsValid ? now.TicksSince(tick) : 0;
                total += age;
                sample.MaxUnitAgeTicks = math.max(sample.MaxUnitAgeTicks, age);
                sample.Units++;
            }

            sample.MeanUnitAgeTicks = sample.Units > 0 ? (float)total / sample.Units : 0f;
        }

        private static int Army(EntityManager entityManager, Entity prefab, int count, Base home, Base enemy,
            in TerrainHeight terrain)
        {
            var forward = math.normalizesafe(new float3(enemy.Position.x - home.Position.x, 0f,
                enemy.Position.z - home.Position.z));
            var right = new float3(forward.z, 0f, -forward.x);
            var columns = (int)math.ceil(math.sqrt(count));
            var front = home.Position + forward * FrontDistance;

            for (var i = 0; i < count; i++)
            {
                var column = i % columns - (columns - 1) * 0.5f;
                var row = i / columns;
                var position = front + right * (column * Spacing) + forward * (row * Spacing);
                position.y = terrain.Height(position.xz);
                Cheats.Spawn(entityManager, prefab, home.Faction, position);
            }

            // Spawned units need their entity ids, so the orders go out once the batch exists.
            using var spawned = SpawnedUnits(entityManager, prefab, home.Faction);
            var commands = entityManager.GetBuffer<PlayerCommand>(PlayerOf(entityManager, home.Faction));
            foreach (var unit in spawned)
            {
                commands.Add(new PlayerCommand { Type = CommandType.AttackMove, Unit = unit, Position = enemy.Position });
            }

            return count;
        }

        private static NativeArray<Entity> SpawnedUnits(EntityManager entityManager, Entity prefab, byte faction)
        {
            var typeId = entityManager.GetComponentData<EntityInfo>(prefab).TypeId;
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<UnitTag, EntityInfo, Faction>()
                .Build(entityManager);
            using var units = query.ToEntityArray(Allocator.Temp);
            var matches = new NativeList<Entity>(units.Length, Allocator.Temp);
            foreach (var unit in units)
            {
                var sameType = entityManager.GetComponentData<EntityInfo>(unit).TypeId == typeId;
                if (sameType && entityManager.GetComponentData<Faction>(unit).Value == faction)
                {
                    matches.Add(unit);
                }
            }

            return matches.ToArray(Allocator.Temp);
        }

        private readonly struct Base
        {
            public readonly byte Faction;
            public readonly float3 Position;

            public Base(byte faction, float3 position)
            {
                Faction = faction;
                Position = position;
            }
        }

        // A player's base is where its first building stands; the opponent is the farthest hostile base.
        private static bool TryFindOpposingBases(EntityManager entityManager, out Base first, out Base second)
        {
            first = second = default;
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<BuildingTag, Faction, LocalTransform>()
                .Build(entityManager);
            using var buildings = query.ToEntityArray(Allocator.Temp);
            var lowest = byte.MaxValue;
            foreach (var building in buildings)
            {
                var faction = entityManager.GetComponentData<Faction>(building).Value;
                if (faction != 0 && faction < lowest)
                {
                    lowest = faction;
                    first = new Base(faction, entityManager.GetComponentData<LocalTransform>(building).Position);
                }
            }

            PlayWorld.TryGetSingleton(entityManager, out FactionRelations relations);
            var farthest = 0f;
            foreach (var building in buildings)
            {
                var faction = entityManager.GetComponentData<Faction>(building).Value;
                var position = entityManager.GetComponentData<LocalTransform>(building).Position;
                var distance = math.distance(first.Position.xz, position.xz);
                if (relations.IsHostile(first.Faction, faction) && distance > farthest)
                {
                    farthest = distance;
                    second = new Base(faction, position);
                }
            }

            return farthest > 0f;
        }

        private static Entity PlayerOf(EntityManager entityManager, byte faction)
        {
            foreach (var player in Cheats.Players(entityManager))
            {
                if (entityManager.GetComponentData<Player>(player).Faction == faction)
                {
                    return player;
                }
            }

            return Entity.Null;
        }
    }
}
