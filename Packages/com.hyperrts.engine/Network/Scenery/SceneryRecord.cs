using Unity.Mathematics;

namespace HyperRTS.Network.Scenery
{
    /// <summary>The server's record of one piece of scenery in the <see cref="SceneryLedger"/>.</summary>
    public struct SceneryRecord
    {
        public ulong Id;
        public float3 Position;
        public byte State;

        /// <summary>Teams (a bit each) that last saw another state.</summary>
        public ushort Stale;

        /// <summary>Ledger frame of the last change; a team seeing it that frame witnessed it.</summary>
        public uint Changed;

        /// <summary>On the ledger's list of records some team hasn't caught up with.</summary>
        public bool Listed;

        public readonly SceneryChange Change => new() { Id = Id, State = State };
    }
}
