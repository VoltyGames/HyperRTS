using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.SceneManagement;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>Starts, reads and stops replay recording in an authoritative world (single player or server).</summary>
    public static class ReplayRecorder
    {
        public const float DefaultSampleRate = 10f;
        public const float DefaultKeyframeInterval = 10f;

        /// <summary>Starts a new recording, discarding any running one; the first sample is taken this frame.</summary>
        public static void Start(World world, float sampleRate = DefaultSampleRate,
            float keyframeInterval = DefaultKeyframeInterval)
        {
            Discard(world);
            world.EntityManager.CreateSingleton(ReplayRecording.Create(sampleRate, keyframeInterval));
        }

        public static bool IsRecording(World world)
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayRecording>());
            return query.HasSingleton<ReplayRecording>();
        }

        /// <summary>A copy of what is recorded so far, with the players and result; recording goes on.</summary>
        public static Replay Snapshot(World world)
        {
            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayRecording>());
            var recording = query.GetSingleton<ReplayRecording>();
            var replay = new Replay
            {
                ScenePath = SceneManager.GetActiveScene().path,
                SampleRate = 1f / recording.SampleInterval,
            };

            replay.CopySamples(recording.Stream);
            AddPlayers(entityManager, replay);
            using var match = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MatchState>());
            match.TryGetSingleton(out replay.Result);
            return replay;
        }

        /// <summary>Ends the recording and returns it.</summary>
        public static Replay Stop(World world)
        {
            var replay = Snapshot(world);
            Discard(world);
            return replay;
        }

        private static void Discard(World world)
        {
            var entityManager = world.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ReplayRecording>());
            if (query.TryGetSingletonEntity<ReplayRecording>(out var entity))
            {
                entityManager.GetComponentData<ReplayRecording>(entity).Dispose();
                entityManager.DestroyEntity(entity);
            }
        }

        private static void AddPlayers(EntityManager entityManager, Replay replay)
        {
            using var relationsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            relationsQuery.TryGetSingleton(out FactionRelations relations);
            using var playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Player>());
            using var players = playerQuery.ToComponentDataArray<Player>(Allocator.Temp);
            foreach (var player in players)
            {
                replay.Players.Add(new ReplayPlayerInfo
                {
                    Faction = player.Faction,
                    Team = relations.TeamOf(player.Faction),
                    Name = player.Name.ToString(),
                    Color = player.Color,
                });
            }

            replay.Players.Sort((a, b) => a.Faction.CompareTo(b.Faction));
        }
    }
}
