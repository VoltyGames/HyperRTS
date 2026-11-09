using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Netcode.NetcodeTime;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>What the presented world's connections carry and how fresh its units' snapshots are, for the Play page.</summary>
    internal static class NetworkTraffic
    {
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

        /// <summary>Reads the presented (client) world: transport receive rates and how old each unit's latest snapshot is.</summary>
        public static bool TryRead(out Sample sample)
        {
            sample = default;
            if (!PlayWorld.TryGet(WorldSystemFilterFlags.ClientSimulation, out var entityManager))
            {
                return false;
            }

            if (!PlayWorld.TryGetSingleton(entityManager, out NetworkStreamDriver driver)
                || !PlayWorld.TryGetSingleton(entityManager, out NetworkTime time)
                || !PlayWorld.TryGetSingleton(entityManager, out ClientServerTickRate tickRate))
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
                var stats = store.GetDriverRO(id).GetStatistics();
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
    }
}
