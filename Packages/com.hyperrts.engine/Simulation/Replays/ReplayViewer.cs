using System;
using HyperRTS.Core;
using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// Plays a <see cref="Replay"/> in a local world. Load <see cref="Replay.ScenePath"/> first; playback starts once
    /// its map has streamed in. Gameplay phases are off meanwhile, so nothing simulates, fights or thinks.
    /// </summary>
    public static class ReplayViewer
    {
        private static readonly Type[] GameplayGroups =
        {
            typeof(OrderSystemGroup), typeof(MovementSystemGroup), typeof(CombatSystemGroup),
            typeof(ProductionSystemGroup), typeof(LifecycleSystemGroup),
        };

        public static void Begin(World world, Replay replay)
        {
            if (replay.Frames.Length == 0)
            {
                throw new ArgumentException("The replay has no samples.", nameof(replay));
            }

            Stop(world);
            SetGameplay(world, false);
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, ReplayPlaybackState.Create(replay));
            entityManager.AddComponentData(entity, new ReplayPlayback
            {
                Duration = replay.Duration,
                Speed = 1f,
                Playing = true,
            });
        }

        public static bool IsPlaying(World world)
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayPlaybackState>());
            return query.HasSingleton<ReplayPlaybackState>();
        }

        /// <summary>Ends playback and turns gameplay back on; the world keeps the replayed entities.</summary>
        public static void Stop(World world)
        {
            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayPlaybackState>());
            if (!query.TryGetSingletonEntity<ReplayPlaybackState>(out var entity))
            {
                return;
            }

            entityManager.GetComponentData<ReplayPlaybackState>(entity).Dispose();
            entityManager.DestroyEntity(entity);
            SetGameplay(world, true);
        }

        private static void SetGameplay(World world, bool enabled)
        {
            foreach (var type in GameplayGroups)
            {
                var group = world.GetExistingSystemManaged(type);
                if (group != null)
                {
                    group.Enabled = enabled;
                }
            }
        }
    }
}
