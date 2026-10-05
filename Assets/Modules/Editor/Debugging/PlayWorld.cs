using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>The running game world for Play-mode tools, with its jobs completed so data is safe to read.</summary>
    internal static class PlayWorld
    {
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
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.TryGetSingleton(out value);
        }
    }
}
