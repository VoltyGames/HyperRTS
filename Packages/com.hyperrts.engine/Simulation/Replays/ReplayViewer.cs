using System;
using HyperRTS.Core;
using HyperRTS.Simulation.Vision;
using Unity.Entities;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>
    /// Plays a <see cref="Replay"/> in a local world. Load <see cref="Replay.ScenePath"/> first; playback starts once
    /// its map has streamed in. Gameplay phases are off meanwhile, so nothing simulates, fights or thinks.
    /// </summary>
    public static class ReplayViewer
    {
        public static void Begin(World world, Replay replay)
        {
            if (replay.Frames.Length == 0)
            {
                throw new ArgumentException("The replay has no samples.", nameof(replay));
            }

            Stop(world);
            var data = ReplayPlaybackState.Create(replay);
            data.GameplayEnabled = GameplayPhases.PauseAll(world);
            var entityManager = world.EntityManager;
            LocalFogViewSystem.SetRevealAll(entityManager, true);
            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, data);
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

        /// <summary>Ends playback and restores the gameplay phases as they were; the world keeps the replayed entities.</summary>
        public static void Stop(World world)
        {
            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayPlaybackState>());
            if (!query.TryGetSingletonEntity<ReplayPlaybackState>(out var entity))
            {
                return;
            }

            var data = entityManager.GetComponentData<ReplayPlaybackState>(entity);
            data.Dispose();
            entityManager.DestroyEntity(entity);
            GameplayPhases.Restore(world, data.GameplayEnabled);
            LocalFogViewSystem.SetRevealAll(entityManager, false);
        }
    }
}
