using Unity.Entities;

namespace HyperRTS.Presentation.Rendering.Parts
{
    /// <summary>
    /// A copy of a model prefab (a section's holed or broken model, a later damage stage) placed in its root while it
    /// shows; <see cref="ModelCopies"/> keeps the list.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ModelCopy : IBufferElementData
    {
        public Entity Model;
        public Entity Copy;
    }
}
