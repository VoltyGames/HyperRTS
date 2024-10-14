using Unity.Entities;

namespace HyperRTS.Core.Units
{
    /// <summary>
    /// Movement speed component for entities that have movement speed.
    /// </summary>
    public struct MovementSpeed : IComponentData
    {
        public float Value;
    }
}
