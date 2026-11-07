using System;
using HyperRTS.Simulation.Match;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>One slot of the launcher's table.</summary>
    [Serializable]
    public class LaunchSlot
    {
        public LaunchSlotKind kind;

        /// <summary>The game's side id; 0 picks one at random.</summary>
        public byte side;

        /// <summary>1 and up; 0 plays on no team.</summary>
        public int team;

        public AIDifficulty difficulty = AIDifficulty.Normal;
    }
}
