using Unity.Entities;

namespace HyperRTS.Simulation.Status
{
    /// <summary>One effect waiting for <see cref="StatusEffectSystem"/> to apply it to <see cref="Target"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct StatusRequest : IBufferElementData
    {
        public Entity Target;

        /// <summary>The status effect prefab.</summary>
        public Entity Definition;

        public Entity Source;
        public byte SourceFaction;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="StatusRequest"/>s.</summary>
    public struct StatusQueue : IComponentData { }
}
