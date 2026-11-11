using System;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>What a bot match run plays: how many matches, the players, their difficulty, the game speed and the time limit.</summary>
    [Serializable]
    public class BotMatchSettings
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
}
