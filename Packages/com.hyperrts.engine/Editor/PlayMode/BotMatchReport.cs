using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Network.Session;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>A bot match run's settings and the result of each match, written to Logs/BotMatches as JSON.</summary>
    [Serializable]
    internal class BotMatchReport
    {
        private const string Folder = "Logs/BotMatches";

        public string scene;
        public string started;
        public BotMatchSettings settings;
        public List<Match> matches = new();

        /// <summary>One match's outcome.</summary>
        [Serializable]
        public class Match
        {
            public uint seed;
            public float minutes;
            public bool timedOut;
            public int winningTeam;
            public List<int> winningSides = new();
            public List<ServerReport.PlayerResult> players = new();

            public string Winner()
            {
                if (timedOut)
                {
                    return "none (time limit)";
                }

                return winningSides.Count == 0 ? "draw" : Sides;
            }

            public string Describe()
            {
                if (timedOut)
                {
                    return $"no winner after {minutes:F1} min";
                }

                return winningSides.Count == 0 ? $"draw at {minutes:F1} min" : $"{Sides} won at {minutes:F1} min";
            }

            private string Sides => $"side {string.Join("+", winningSides)}";
        }

        public string Summary()
        {
            var wins = new SortedDictionary<int, int>();
            var decidedMinutes = new List<float>();
            foreach (var match in matches)
            {
                if (match.winningSides.Count == 0)
                {
                    continue;
                }

                decidedMinutes.Add(match.minutes);
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

            var decided = decidedMinutes.Count > 0 ? $", {decidedMinutes.Min():F1}-{decidedMinutes.Max():F1} min" : "";
            return $"{matches.Count} matches: {string.Join(", ", parts)}{decided}";
        }

        public string Write()
        {
            Directory.CreateDirectory(Folder);
            var name = $"{Path.GetFileNameWithoutExtension(scene)}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var path = Path.Combine(Folder, name).Replace('\\', '/');
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
            return path;
        }
    }
}
