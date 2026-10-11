using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>A player start in map space with the slot and team the symmetry gave it.</summary>
    public sealed class MapSpawn
    {
        public MapSpawn(Vector2 position, float yaw, int slot, int team, int copy)
        {
            Position = position;
            Yaw = yaw;
            Slot = slot;
            Team = team;
            Copy = copy;
        }

        public Vector2 Position { get; }

        public float Yaw { get; }

        /// <summary>Player slot from 1, the faction number of the start kit's owner.</summary>
        public int Slot { get; }

        public int Team { get; }

        public int Copy { get; }
    }
}
