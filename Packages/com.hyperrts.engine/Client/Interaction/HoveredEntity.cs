using Unity.Entities;

namespace HyperRTS.Client.Interaction
{
    /// <summary>Written by input, read by overlays: the commandable entity under the pointer, or Entity.Null.</summary>
    public struct HoveredEntity : IComponentData
    {
        public Entity Value;
    }
}
