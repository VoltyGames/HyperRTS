using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>
    /// Plays the open map AI against AI, match after match, and writes the results to Logs/BotMatches. Each match moves
    /// the baked sides one slot on, so every side plays every start. Editor Play mode only.
    /// </summary>
    internal static class BotMatches
    {
        private const string Folder = "Logs/BotMatches";

        [Serializable]
        public class Settings
        {
            public int matches = 4;
            public int players = 2;
            public AIDifficulty difficulty = AIDifficulty.Normal;
            public float speed = 8f;
            public float timeLimitMinutes = 40f;
        }

        [Serializable]
        private class PlayerResult
        {
            public int slot;
            public int side;
            public int team;
            public int unitsBuilt;
            public int unitsLost;
            public int unitsKilled;
            public int buildingsBuilt;
            public int buildingsLost;
            public int buildingsDestroyed;
            public int resourcesGathered;
        }

        [Serializable]
        private class MatchResult
        {
            public uint seed;
            public float minutes;
            public bool timedOut;
            public int winningTeam;
            public List<int> winningSides = new();
            public List<PlayerResult> players = new();
        }

        [Serializable]
        private class Report
        {
            public string scene;
            public string started;
            public Settings settings;
            public List<MatchResult> matches = new();
        }

        private static Report _report;
        private static byte[] _bakedSides;
        private static int _current = -1;
        private static bool _setupSeen;

        public static bool IsRunning => _report != null;

        public static string Status { get; private set; } = "";

        public static void Start(Settings settings)
        {
            if (IsRunning || !PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                return;
            }

            _bakedSides = BakedSides(entityManager, settings.players);
            _report = new Report
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

            var path = Write(_report);
            Status = $"{(stopped ? "Stopped. " : "")}{Summary(_report)}. Report: {path}";
            Debug.Log($"[HyperRTS] Bot matches done: {Status}");
            LocalGameSpeed.Reset();
            MatchSetupRequest.Clear();
            _report = null;
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
            foreach (var player in Cheats.Players(entityManager))
            {
                var faction = entityManager.GetComponentData<Player>(player).Faction;
                if (faction >= 1 && faction <= players)
                {
                    sides[faction - 1] = entityManager.GetComponentData<PlayerSide>(player).Value;
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

            _report.matches.Add(new MatchResult { seed = seed });
            _setupSeen = false;
            MatchSetupRequest.Set(setup);
            NetworkSession.StartLocal(SessionScene.ReloadActive);
        }

        private static void Tick()
        {
            if (!PlayWorld.TryGetAuthoritative(out var entityManager) ||
                !PlayWorld.TryGetSingleton(entityManager, out MatchSetup setup) ||
                !PlayWorld.TryGetSingleton(entityManager, out MatchState state))
            {
                return;
            }

            // The world from before the restart still answers until the new scene has applied its setup.
            _setupSeen |= setup.Applied && setup.Seed == _report.matches[_current].seed;
            if (!_setupSeen)
            {
                return;
            }

            LocalGameSpeed.SetScale(_report.settings.speed);
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
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<Player, PlayerSide, PlayerStats>()
                .Build(entityManager);
            using var players = query.ToEntityArray(Allocator.Temp);
            using var relations = new EntityQueryBuilder(Allocator.Temp).WithAll<FactionRelations>().Build(entityManager);
            var teams = relations.GetSingleton<FactionRelations>();
            foreach (var player in players)
            {
                var faction = entityManager.GetComponentData<Player>(player).Faction;
                var side = entityManager.GetComponentData<PlayerSide>(player).Value;
                var team = teams.GetTeam(faction);
                var stats = entityManager.GetComponentData<PlayerStats>(player);
                result.players.Add(new PlayerResult
                {
                    slot = faction,
                    side = side,
                    team = team,
                    unitsBuilt = stats.UnitsBuilt,
                    unitsLost = stats.UnitsLost,
                    unitsKilled = stats.UnitsKilled,
                    buildingsBuilt = stats.BuildingsBuilt,
                    buildingsLost = stats.BuildingsLost,
                    buildingsDestroyed = stats.BuildingsDestroyed,
                    resourcesGathered = stats.ResourcesGathered,
                });
                if (team == result.winningTeam && result.winningTeam != 0)
                {
                    result.winningSides.Add(side);
                }
            }

            result.players.Sort((a, b) => a.slot.CompareTo(b.slot));
            Debug.Log($"[HyperRTS] Bot match {_current + 1}: {Describe(result)}");
        }

        private static string Describe(MatchResult result)
        {
            if (result.timedOut)
            {
                return $"no winner after {result.minutes:F1} min";
            }

            return result.winningSides.Count == 0
                ? $"draw at {result.minutes:F1} min"
                : $"side {string.Join("+", result.winningSides)} won at {result.minutes:F1} min";
        }

        private static string Summary(Report report)
        {
            var wins = new SortedDictionary<int, int>();
            var minutes = new List<float>();
            foreach (var match in report.matches)
            {
                if (match.winningSides.Count == 0)
                {
                    continue;
                }

                minutes.Add(match.minutes);
                foreach (var side in match.winningSides)
                {
                    wins[side] = wins.TryGetValue(side, out var count) ? count + 1 : 1;
                }
            }

            var parts = new List<string>();
            foreach (var pair in wins)
            {
                parts.Add($"side {pair.Key} won {pair.Value}");
            }

            var decided = minutes.Count > 0 ? $", {minutes.Min():F1}-{minutes.Max():F1} min" : "";
            return $"{report.matches.Count} matches: {string.Join(", ", parts)}{decided}";
        }

        private static string Write(Report report)
        {
            Directory.CreateDirectory(Folder);
            var name = $"{Path.GetFileNameWithoutExtension(report.scene)}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var path = Path.Combine(Folder, name).Replace('\\', '/');
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            return path;
        }
    }
}
