using Unity.Entities;

namespace HyperRTS.Simulation.Fields
{
    /// <summary>A field this entity is inside, one per field id, sorted by id. Rewritten by <see cref="AreaFieldSystem"/>.</summary>
    [InternalBufferCapacity(0)]
    public struct FieldPresence : IBufferElementData
    {
        public int FieldId;

        /// <summary>The entity projecting the field (the lowest index when several overlap).</summary>
        public Entity Source;

        public byte SourceFaction;
    }
}
