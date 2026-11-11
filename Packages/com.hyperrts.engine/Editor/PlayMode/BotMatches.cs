using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>
    /// Plays the open map AI against AI, match after match, and writes the results to Logs/BotMatches. Each match moves
    /// the baked sides one slot on, so every side plays every start. Editor Play mode only.
    /// </summary>
    internal static partial class BotMatches
    {
        private const double TickSeconds = 0.25;

        /// <summary>Every match finished since the editor opened, oldest first.</summary>
        [NoAutoStaticsCleanup]
        public static readonly List<Result> Results = new();

        [AutoStaticsCleanup]
        private static BotMatchReport _report;
        [AutoStaticsCleanup]
        private static byte[] _bakedSides;
        [AutoStaticsCleanup]
        private static int _current = -1;
        [AutoStaticsCleanup]
        private static bool _setupSeen;
        [AutoStaticsCleanup]
        private static double _nextTick;

        /// <summary>One finished match, for the Play page's table.</summary>
        public class Result
        {
            public string run;
            public int match;
            public string winner;
            public float minutes;
            public string sides;
        }

        public static bool IsRunning => _report != null;

        [AutoStaticsCleanup]
        public static string Status { get; private set; } = "";

        public static void Start(BotMatchSettings settings)
        {
            if (IsRunning || !PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return;
            }

            _bakedSides = BakedSides(entityManager, settings.players);
            _report = new BotMatchReport
            {
                scene = SceneManager.GetActiveScene().path,
                started = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                settings = settings,
            };
            _current = -1;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StopOnExit;
            Next();
        }

        public static void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= StopOnExit;
            // A match stopped before it ended has no result.
            var stopped = _current < _report.matches.Count && _report.matches[_current].players.Count == 0;
            if (stopped)
            {
                _report.matches.RemoveAt(_current);
            }

            var path = _report.Write();
            Status = $"{(stopped ? "Stopped. " : "")}{_report.Summary()}. Report: {path}";
            Debug.Log($"[HyperRTS] Bot matches done: {Status}");
            ResetSpeed();
            _report = null;
        }

        // The last match's world outlives the batch, so it goes back to normal speed.
        private static void ResetSpeed()
        {
            if (PlayWorld.TryGetAuthoritative(out var entityManager) &&
                PlayWorld.TryGetSingleton(entityManager, out GameSpeed speed))
            {
                PlayWorld.SetSingleton(entityManager, speed.WithScale(1f));
            }
        }

        private static void StopOnExit(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                Stop();
            }
        }

        // Sides as the scene bakes them, by slot; the rotation for match k starts at baked slot k.
        private static byte[] BakedSides(EntityManager entityManager, int players)
        {
            var sides = new byte[players];
            foreach (var player in PlayWorld.Players(entityManager))
            {
                var owner = entityManager.GetComponentData<Player>(player).Owner;
                if (owner >= 1 && owner <= players)
                {
                    sides[owner - 1] = entityManager.GetComponentData<PlayerSide>(player).Value;
                }
            }

            return sides;
        }

        private static void Next()
        {
            _current++;
            if (_current >= _report.settings.matches)
            {
                Stop();
                return;
            }

            var seed = (uint)(_current + 1);
            var setup = MatchSetup.Create();
            setup.Seed = seed;
            setup.SpeedScale = _report.settings.speed;
            for (var slot = 0; slot < _report.settings.players; slot++)
            {
                setup.Slots.Add(new SlotSetup
                {
                    Open = true,
                    Control = PlayerControl.AI,
                    Team = (byte)(slot + 1),
                    Color = (Vector4)PlayerSlot.Palette[slot % PlayerSlot.Palette.Length].linear,
                    Difficulty = _report.settings.difficulty,
                    Side = _bakedSides[(slot + _current) % _bakedSides.Length],
                });
            }

            _report.matches.Add(new BotMatchReport.Match { seed = seed });
            _setupSeen = false;
            NetworkSession.StartLocal(SessionScene.ReloadActive.WithSetup(setup));
        }

        // Polled at the Play page's rate: reading the world completes all of its jobs.
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextTick)
            {
                return;
            }

            _nextTick = EditorApplication.timeSinceStartup + TickSeconds;
            if (!PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return;
            }

            if (!PlayWorld.TryGetSingleton(entityManager, out MatchSetup setup))
            {
                return;
            }

            if (!PlayWorld.TryGetSingleton(entityManager, out MatchState state))
            {
                return;
            }

            // The world from before the restart still answers until the new scene has applied its setup.
            if (!_setupSeen)
            {
                _setupSeen = setup.Applied && setup.Seed == _report.matches[_current].seed;
                if (!_setupSeen)
                {
                    return;
                }

            }

            var minutes = (float)entityManager.World.Time.ElapsedTime / 60f;
            var timedOut = minutes >= _report.settings.timeLimitMinutes;
            Status = $"Match {_current + 1}/{_report.settings.matches}: {minutes:F1} min";
            if (state.Phase != MatchPhase.Ended && !timedOut)
            {
                return;
            }

            Record(entityManager, state, minutes, timedOut);
            Next();
        }

        private static void Record(EntityManager entityManager, in MatchState state, float minutes, bool timedOut)
        {
            var result = _report.matches[_current];
            result.minutes = minutes;
            result.timedOut = timedOut;
            result.winningTeam = timedOut ? 0 : state.WinningTeam;
            result.players.AddRange(ServerReport.PlayerResult.Read(entityManager));
            foreach (var player in result.players)
            {
                if (player.team == result.winningTeam && result.winningTeam != 0)
                {
                    result.winningSides.Add(player.side);
                }
            }

            Debug.Log($"[HyperRTS] Bot match {_current + 1}: {result.Describe()}");
            Results.Add(new Result
            {
                run = _report.started,
                match = _current + 1,
                winner = result.Winner(),
                minutes = minutes,
                sides = string.Join(" v ", result.players.GroupBy(player => player.team)
                    .Select(team => string.Join("+", team.Select(player => player.side)))),
            });
        }
    }
}
