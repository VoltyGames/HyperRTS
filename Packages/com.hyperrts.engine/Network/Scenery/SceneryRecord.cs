using Unity.Mathematics;

namespace HyperRTS.Network.Scenery
{
    /// <summary>The server's record of one piece of scenery in the <see cref="SceneryTracker"/>.</summary>
    public struct SceneryRecord
    {
        public ulong Id;
        public float3 Position;
        public byte State;
        public byte Fall;
        public bool Cleared;

        /// <summary>Teams, a bit each, that last saw another state.</summary>
        public ushort Stale;

        /// <summary>Send frame of the last change. A team seeing it that frame witnessed it.</summary>
        public uint Changed;

        /// <summary>Whether some team hasn't caught up with it yet.</summary>
        public bool Listed;

        public readonly SceneryChange Change => new() { Id = Id, State = State, Fall = Fall, Cleared = Cleared };
    }
}
