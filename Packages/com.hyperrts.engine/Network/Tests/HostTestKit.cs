using System;
using System.Collections.Generic;
using HyperRTS.Network.Joining;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Tests;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Network.Tests
{
    /// <summary>
    /// A single-world host as <see cref="NetworkSession"/> builds it and a remote client joined to it over IPC, each
    /// with the same match, ticked together at the server rate.
    /// </summary>
    public sealed class HostTestKit : IDisposable
    {
        public const float FrameTime = 1f / 60f;

        // Rendering, input and the game stay out.
        [NoAutoStaticsCleanup]
        private static readonly HashSet<string> Assemblies = new()
        {
            "UnityEngine.EntitiesModule", "Unity.Entities", "Unity.Transforms", "Unity.NetCode", "HyperRTS.Simulation", "HyperRTS.Network",
        };

        // IPC ports stay unique across tests, so a closing test's port is never reused.
        [NoAutoStaticsCleanup]
        private static ushort _nextPort = 47900;

        private readonly bool _ranInBackground;
        private double _elapsed;

        public TestWorld Host { get; }
        public TestWorld Client { get; }

        public HostTestKit(params byte[] teams) : this(0, teams)
        {
        }

        private HostTestKit(int clientPacketLoss, byte[] teams)
        {
            NetworkSession.ResetStatics();

            // Netcode logs an error for a session that would stall unfocused.
            _ranInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var port = _nextPort++;
            var host = SessionWorlds.CreateHost(Keep(SessionWorlds.HostSystems()));
            StopEditorClock(host);
            Host = new TestWorld(host);
            Host.CreateMatch(teams);
            if (!SessionWorlds.TryListen(host, port, new IpcDrivers(0)))
            {
                throw new InvalidOperationException($"The test host could not listen on IPC port {port}.");
            }

            var client = SessionWorlds.CreateClient(Keep(SessionWorlds.ClientSystems()));
            StopEditorClock(client);
            Client = new TestWorld(client);
            Client.CreateMatch(teams);

            // One process shares one token, which would hand the client the host's slot.
            using (var token = Client.EntityManager.CreateEntityQuery(typeof(JoinToken)))
            {
                token.SetSingleton(new JoinToken { Value = 2 });
            }

            // Who joins first depends on frame timing, so each asks for its slot as a lobby would.
            Prefer(Host, 1);
            Prefer(Client, 2);

            SessionWorlds.Connect(client, NetworkEndpoint.LoopbackIpv4.WithPort(port), new IpcDrivers(clientPacketLoss));
        }

        /// <summary>A session whose client drops this percentage of the packets it sends and receives.</summary>
        public static HostTestKit WithPacketLoss(int percent, params byte[] teams) => new(percent, teams);

        public void Tick(int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                _elapsed += FrameTime;
                Update(Host.World);
                Update(Client.World);
            }
        }

        /// <summary>Ticks until <paramref name="done"/> holds or <paramref name="cap"/> seconds pass; returns whether it holds.</summary>
        public bool RunUntil(Func<bool> done, float cap)
        {
            for (var t = 0f; t < cap && !done(); t += FrameTime)
            {
                Tick();
            }

            return done();
        }

        /// <summary>Both players hold their slot and the match has started.</summary>
        public bool Joined()
        {
            if (!Host.Singleton<MatchState>().Started)
            {
                return false;
            }

            return HasLocalPlayer(Host) && HasLocalPlayer(Client);
        }

        public static Entity LocalPlayer(TestWorld world)
        {
            using var query = world.Query(typeof(LocalPlayer));
            return query.GetSingletonEntity();
        }

        /// <summary>A unit prefab made a ghost in both worlds, as <see cref="PlayerGhostSystem"/> does players.</summary>
        public Entity GhostUnitPrefab(byte owner, string name)
        {
            var config = new GhostPrefabCreation.Config
            {
                Name = name,
                SupportedGhostModes = GhostModeMask.Interpolated,
                DefaultGhostMode = GhostMode.Interpolated,
                OptimizationMode = GhostOptimizationMode.Dynamic,
            };
            var prefab = Entity.Null;
            foreach (var world in new[] { Client, Host })
            {
                prefab = world.SpawnUnit(owner, float3.zero, new TestUnit { Name = name });
                GhostPrefabCreation.ConvertToGhostPrefab(world.EntityManager, prefab, config);
            }

            return prefab;
        }

        public void Dispose()
        {
            Client.Dispose();
            Host.Dispose();
            Application.runInBackground = _ranInBackground;
        }

        private static void Prefer(TestWorld world, byte owner)
        {
            using var query = world.Query(typeof(JoinPreference));
            query.SetSingleton(new JoinPreference { Owner = owner });
        }

        private static bool HasLocalPlayer(TestWorld world)
        {
            using var query = world.Query(typeof(LocalPlayer));
            return query.CalculateEntityCount() == 1;
        }

        private void Update(World world)
        {
            world.SetTime(new TimeData(_elapsed, FrameTime));
            world.Update();
        }

        // Netcode's time system orders against the editor's clock, which stays off so frames get a fixed time step.
        private static void StopEditorClock(World world) =>
            world.GetExistingSystemManaged<UpdateWorldTimeSystem>().Enabled = false;

        private static NativeList<SystemTypeIndex> Keep(NativeList<SystemTypeIndex> systems)
        {
            for (var i = systems.Length - 1; i >= 0; i--)
            {
                var type = TypeManager.GetSystemType(systems[i]);
                var kept = Assemblies.Contains(type.Assembly.GetName().Name) || type == typeof(UpdateWorldTimeSystem);
                if (!kept)
                {
                    systems.RemoveAt(i);
                }
            }

            return systems;
        }

        private readonly struct IpcDrivers : INetworkStreamDriverConstructor
        {
            private readonly int _packetLoss;

            public IpcDrivers(int packetLoss) => _packetLoss = packetLoss;

            public void CreateServerDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug) =>
                DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driverStore, netDebug,
                    DefaultDriverBuilder.GetNetworkServerSettings());

            public void CreateClientDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug)
            {
                var settings = DefaultDriverBuilder.GetNetworkClientSettings();
                if (_packetLoss == 0)
                {
                    DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug, settings);
                    return;
                }

                // A fixed seed keeps the drops the same on every run.
                settings.WithSimulatorStageParameters(maxPacketCount: 256, mode: ApplyMode.AllPackets,
                    packetDropPercentage: _packetLoss, randomSeed: 7);
                var lossy = new NetworkDriverStore.NetworkDriverInstance
                {
                    driver = NetworkDriver.Create(new IPCNetworkInterface(), settings),
                };
                DefaultDriverBuilder.CreateClientSimulatorPipelines(ref lossy);
                driverStore.RegisterDriver(TransportType.IPC, lossy);
            }
        }
    }
}
