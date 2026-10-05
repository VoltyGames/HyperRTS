using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>The running game world for Play-mode tools, with its jobs completed so data is safe to read.</summary>
    internal static class PlayWorld
    {
        private static readonly Dictionary<Type, EntityQuery> Singletons = new();
        private static World _world;

        public static bool TryGet(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            entityManager = default;
            if (!Application.isPlaying || world == null || !world.IsCreated)
            {
                return false;
            }

            entityManager = world.EntityManager;
            entityManager.CompleteAllTrackedJobs();
            return true;
        }

        public static bool TryGetSingleton<T>(EntityManager entityManager, out T value) where T : unmanaged, IComponentData
        {
            // Queries belong to their world, so the cache resets whenever Play mode starts a new one.
            if (_world != entityManager.World)
            {
                _world = entityManager.World;
                Singletons.Clear();
            }

            if (!Singletons.TryGetValue(typeof(T), out var query))
            {
                Singletons[typeof(T)] = query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            }

            return query.TryGetSingleton(out value);
        }
    }
}
