using System;
using System.Collections.Generic;
using HyperRTS.Network.Players;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Netcode;
using Unity.Networking.Transport;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// A single-world host as <see cref="NetworkSession"/> builds it and a remote client joined to it over IPC, each
    /// with the same match, ticked together at the server rate.
    /// </summary>
    public sealed class HostTestKit : IDisposable
    {
        public const float FrameTime = 1f / 60f;

        // Rendering, input and the game stay out; the editor's clock does too, so frames get a fixed time step.
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

        public HostTestKit(params byte[] teams)
        {
            NetworkSession.ResetStatics();

            // Netcode logs an error for a session that would stall unfocused.
            _ranInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var port = _nextPort++;
            var host = SessionWorlds.CreateHost(Keep(SessionWorlds.HostSystems()));
            Host = new TestWorld(host);
            Host.CreateMatch(teams);
            if (!SessionWorlds.TryListen(host, port, new IpcDrivers()))
            {
                throw new InvalidOperationException($"The test host could not listen on IPC port {port}.");
            }

            var client = SessionWorlds.CreateClient(Keep(SessionWorlds.ClientSystems()));
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

            SessionWorlds.Connect(client, NetworkEndpoint.LoopbackIpv4.WithPort(port), new IpcDrivers());
        }

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
            using var query = world.EntityManager.CreateEntityQuery(typeof(LocalPlayer));
            return query.GetSingletonEntity();
        }

        public void Dispose()
        {
            Client.Dispose();
            Host.Dispose();
            Application.runInBackground = _ranInBackground;
        }

        private static void Prefer(TestWorld world, byte faction)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(JoinPreference));
            query.SetSingleton(new JoinPreference { Faction = faction });
        }

        private static bool HasLocalPlayer(TestWorld world)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(LocalPlayer));
            return query.CalculateEntityCount() == 1;
        }

        private void Update(World world)
        {
            world.SetTime(new TimeData(_elapsed, FrameTime));
            world.Update();
        }

        private static NativeList<SystemTypeIndex> Keep(NativeList<SystemTypeIndex> systems)
        {
            for (var i = systems.Length - 1; i >= 0; i--)
            {
                if (!Assemblies.Contains(TypeManager.GetSystemType(systems[i]).Assembly.GetName().Name))
                {
                    systems.RemoveAt(i);
                }
            }

            return systems;
        }

        private readonly struct IpcDrivers : INetworkStreamDriverConstructor
        {
            public void CreateServerDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug) =>
                DefaultDriverBuilder.RegisterServerIpcDriver(world, ref driverStore, netDebug,
                    DefaultDriverBuilder.GetNetworkServerSettings());

            public void CreateClientDriver(NetcodeWorld world, ref NetworkDriverStore driverStore, NetDebug netDebug) =>
                DefaultDriverBuilder.RegisterClientIpcDriver(world, ref driverStore, netDebug,
                    DefaultDriverBuilder.GetNetworkClientSettings());
        }
    }
}
