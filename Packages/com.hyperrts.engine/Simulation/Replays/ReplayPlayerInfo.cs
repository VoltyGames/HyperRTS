using Unity.Mathematics;

namespace HyperRTS.Simulation.Replays
{
    /// <summary>A player as recorded in a replay header.</summary>
    public struct ReplayPlayerInfo
    {
        public byte Faction;
        public byte Team;
        public string Name;

        /// <summary>Linear RGBA team colour.</summary>
        public float4 Color;
    }
}
