using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>One entity per player; holds its stockpile, population and command buffer.</summary>
    public struct Player : IComponentData
    {
        public byte Faction;
        public byte Team;
        public FixedString32Bytes Name;

        /// <summary>Linear RGBA team colour.</summary>
        public float4 Color;
    }
}
