using System;
using HyperRTS.Simulation.GameEntities;
using UnityEngine;

namespace HyperRTS.Editor
{
    /// <summary>What <see cref="RTSSceneBuilder"/> creates: where, how big, for how many players, with what.</summary>
    public sealed class RTSSceneSpec
    {
        /// <summary>Project path such as <c>Assets/Maps/Duel.unity</c>; the SubScene is saved next to it.</summary>
        public string ScenePath { get; set; }

        /// <summary>Playable area (X by Z); the ground is sized to match.</summary>
        public Vector2 MapSize { get; set; } = new(200f, 200f);

        /// <summary>Player 1 is the local human, the rest are AI, each on its own team.</summary>
        public int Players { get; set; } = 2;

        /// <summary>Optional building placed for every player on a ring around the centre.</summary>
        public GameEntityAuthoring StartingBase { get; set; }

        /// <summary>Camera and HUD rig; null uses the engine's RTSWorld (a game passes its own variant).</summary>
        public GameObject Rig { get; set; }

        /// <summary>Adds map content to the SubScene before it is saved and closed.</summary>
        public Action<RTSSubScene> Furnish { get; set; }
    }
}
