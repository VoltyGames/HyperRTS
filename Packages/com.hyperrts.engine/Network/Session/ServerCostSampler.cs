using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.NetCode;
using Unity.Profiling;

namespace HyperRTS.Network.Session
{
    /// <summary>Samples what a server costs, once per call: CPU, memory, connections, bytes sent and entities.</summary>
    internal sealed class ServerCostSampler : IDisposable
    {
        private const float Megabyte = 1024f * 1024f;

        private readonly Process _process = Process.GetCurrentProcess();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private ProfilerRecorder _usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private readonly List<float> _cpu = new();
        private readonly List<float> _workingSet = new();
        private readonly List<float> _usedMemoryMB = new();
        private readonly List<float> _connections = new();
        private readonly List<float> _sentKbps = new();
        private readonly List<float> _entities = new();
        private TimeSpan _lastCpu;
        private double _lastSeconds;
        private ulong _lastSent;

        /// <summary>Starts the intervals now, so the first sample covers only what follows.</summary>
        public ServerCostSampler(World server)
        {
            _lastCpu = _process.TotalProcessorTime;
            _lastSent = SentBytes(server.EntityManager);
        }

        public void Record(World server)
        {
            var entityManager = server.EntityManager;
            var seconds = _clock.Elapsed.TotalSeconds;
            var interval = seconds - _lastSeconds;
            _lastSeconds = seconds;

            _process.Refresh();
            var cpu = _process.TotalProcessorTime;
            _cpu.Add((float)((cpu - _lastCpu).TotalSeconds / interval * 100.0));
            _lastCpu = cpu;
            _workingSet.Add(_process.WorkingSet64 / Megabyte);
            _usedMemoryMB.Add(_usedMemory.LastValue / Megabyte);

            var sent = SentBytes(entityManager);
            _sentKbps.Add((float)((sent - _lastSent) * 8 / 1000.0 / interval));
            _lastSent = sent;
            _connections.Add(Connections(entityManager));
            _entities.Add(entityManager.UniversalQuery.CalculateEntityCount());
        }

        public void Summarize(ServerReport report)
        {
            report.samples = _cpu.Count;
            report.cpuPercentOfCore = ServerReport.Stat.Summarize(_cpu);
            report.workingSetMB = ServerReport.Stat.Summarize(_workingSet);
            report.usedMemoryMB = ServerReport.Stat.Summarize(_usedMemoryMB);
            report.connections = ServerReport.Stat.Summarize(_connections);
            report.sentKbps = ServerReport.Stat.Summarize(_sentKbps);
            report.entities = ServerReport.Stat.Summarize(_entities);
        }

        public void Dispose()
        {
            _usedMemory.Dispose();
            _process.Dispose();
        }

        // Driver statistics count from the driver's creation, across every connection it has had.
        private static ulong SentBytes(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamDriver>());
            var store = query.GetSingleton<NetworkStreamDriver>().DriverStore;
            ulong sent = 0;
            for (var id = store.FirstDriver; id < store.LastDriver; id++)
            {
                sent += store.GetDriverRO(id).GetStatistics().TxTotalBytes;
            }

            return sent;
        }

        private static int Connections(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            return query.CalculateEntityCount();
        }
    }
}
