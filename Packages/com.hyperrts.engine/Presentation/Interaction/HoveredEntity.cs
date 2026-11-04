using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Interaction
{
    /// <summary>Written by input, read by overlays: the commandable entity under the pointer, or Entity.Null, and the ground point under it.</summary>
    public struct HoveredEntity : IComponentData
    {
        public Entity Value;

        /// <summary>The terrain point under the pointer; false while the pointer is over the UI or busy.</summary>
        public float3 Ground;

        public bool HasGround;
    }
}
