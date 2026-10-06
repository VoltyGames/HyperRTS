using System;
using System.Linq;
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
        /// <summary>Every phase group in <c>SystemGroups</c> except replay playback's own, so new phases are paused too.</summary>
        private static readonly Type[] GameplayGroups = typeof(OrderSystemGroup).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ComponentSystemGroup)) && type != typeof(ReplaySystemGroup))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        public static void Begin(World world, Replay replay)
        {
            if (replay.Frames.Length == 0)
            {
                throw new ArgumentException("The replay has no samples.", nameof(replay));
            }

            Stop(world);
            var data = ReplayPlaybackState.Create(replay);
            data.GameplayEnabled = PauseGameplay(world);
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
            RestoreGameplay(world, data.GameplayEnabled);
            LocalFogViewSystem.SetRevealAll(entityManager, false);
        }

        /// <summary>Turns the gameplay phases off; returns which were on, one bit per <see cref="GameplayGroups"/> entry.</summary>
        private static ulong PauseGameplay(World world)
        {
            var enabled = 0ul;
            for (var i = 0; i < GameplayGroups.Length; i++)
            {
                var group = world.GetExistingSystemManaged(GameplayGroups[i]);
                if (group == null)
                {
                    continue;
                }

                enabled |= group.Enabled ? 1ul << i : 0ul;
                group.Enabled = false;
            }

            return enabled;
        }

        private static void RestoreGameplay(World world, ulong enabled)
        {
            for (var i = 0; i < GameplayGroups.Length; i++)
            {
                var group = world.GetExistingSystemManaged(GameplayGroups[i]);
                if (group != null)
                {
                    group.Enabled = (enabled & (1ul << i)) != 0;
                }
            }
        }
    }
}
