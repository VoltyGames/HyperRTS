using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Network.Session;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>The settings, result types and report text of the bot match runner.</summary>
    internal static partial class BotMatches
    {
        private const string Folder = "Logs/BotMatches";

        [Serializable]
        public class Settings
        {
            [Min(1)]
            public int matches = 4;

            [Min(2)]
            public int players = 2;

            public AIDifficulty difficulty = AIDifficulty.Normal;

            [Min(1f)]
            public float speed = 8f;

            [Min(1f)]
            public float timeLimitMinutes = 40f;
        }

        [Serializable]
        private class MatchResult
        {
            public uint seed;
            public float minutes;
            public bool timedOut;
            public int winningTeam;
            public List<int> winningSides = new();
            public List<ServerReport.PlayerResult> players = new();
        }

        [Serializable]
        private class Report
        {
            public string scene;
            public string started;
            public Settings settings;
            public List<MatchResult> matches = new();
        }

        /// <summary>One finished match, for the Play page's table.</summary>
        public class Result
        {
            public string run;
            public int match;
            public string winner;
            public float minutes;
            public string sides;
        }

        private static string Winner(MatchResult result)
        {
            if (result.timedOut)
            {
                return "none (time limit)";
            }

            return result.winningSides.Count == 0 ? "draw" : $"side {string.Join("+", result.winningSides)}";
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
