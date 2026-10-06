using System;
using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>The running game worlds for Play-mode tools, with their jobs completed so data is safe to read.</summary>
    internal static class PlayWorld
    {
        private static readonly Dictionary<World, Dictionary<Type, EntityQuery>> Singletons = new();

        /// <summary>The world that owns game state: the server when one runs, else the single-player world. Cheats write here.</summary>
        public static bool TryGetAuthoritative(out EntityManager entityManager) =>
            TryGet(SimulationWorlds.Authoritative, out entityManager);

        /// <summary>The world the local player looks at: the client when one runs, else the single-player world.</summary>
        public static bool TryGetPresented(out EntityManager entityManager) =>
            TryGet(SimulationWorlds.Presented, out entityManager);

        /// <summary>The first running world with one of <paramref name="roles"/>, preferring a networked one.</summary>
        public static bool TryGet(WorldSystemFilterFlags roles, out EntityManager entityManager)
        {
            entityManager = default;
            var world = Application.isPlaying ? Find(roles) : null;
            if (world == null)
            {
                return false;
            }

            entityManager = world.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            return true;
        }

        public static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData
        {
            var queries = QueriesOf(entityManager.World);
            if (!queries.TryGetValue(typeof(T), out var query))
            {
                queries[typeof(T)] = query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            }

            return query.TryGetSingleton(out value);
        }

        // The local world only remains as a fallback: a session replaces it with server and client worlds.
        private static World Find(WorldSystemFilterFlags roles)
        {
            World local = null;
            foreach (var world in World.All)
            {
                var role = RoleOf(world);
                if ((role & roles) == 0)
                {
                    continue;
                }

                if (role != WorldSystemFilterFlags.LocalSimulation)
                {
                    return world;
                }

                local ??= world;
            }

            return local;
        }

        private static WorldSystemFilterFlags RoleOf(World world)
        {
            if (!world.IsCreated || world.IsThinClient())
            {
                return 0;
            }

            if (world.IsServer())
            {
                return WorldSystemFilterFlags.ServerSimulation;
            }

            if (world.IsClient())
            {
                return WorldSystemFilterFlags.ClientSimulation;
            }

            var isGame = (world.Flags & WorldFlags.Game) == WorldFlags.Game;
            return isGame ? WorldSystemFilterFlags.LocalSimulation : 0;
        }

        // Queries belong to their world, so each world keeps its own and disposed worlds are dropped.
        private static Dictionary<Type, EntityQuery> QueriesOf(World world)
        {
            if (Singletons.TryGetValue(world, out var queries))
            {
                return queries;
            }

            var disposed = new List<World>();
            foreach (var cached in Singletons.Keys)
            {
                if (!cached.IsCreated)
                {
                    disposed.Add(cached);
                }
            }

            disposed.ForEach(stale => Singletons.Remove(stale));
            Singletons[world] = queries = new Dictionary<Type, EntityQuery>();
            return queries;
        }
    }
}
