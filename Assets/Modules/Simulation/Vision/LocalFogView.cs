using HyperRTS.Simulation.Match;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>Singleton: the local player's view through the fog. Inactive (nothing hidden) without fog or a local team.</summary>
    public struct LocalFogView : IComponentData
    {
        public FogOfWar Fog;
        public FactionRelations Relations;
        public byte Viewer;
        public bool Active;

        /// <summary>True when an entity of this faction at this position is hidden from the local player.</summary>
        public readonly bool IsHidden(byte faction, float3 position) =>
            Active && Fog.IsHiddenFrom(in Relations, Viewer, faction, position);
    }
}
