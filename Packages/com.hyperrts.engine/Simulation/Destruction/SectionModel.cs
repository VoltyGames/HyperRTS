using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>
    /// The models that show a <see cref="Section"/>, at the same index: standing, breaking and broken. All but
    /// <see cref="Intact"/> bake as entity prefabs, copied in while they show.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SectionModel : IBufferElementData
    {
        public Entity Intact;
        public Entity Broken;
        public Entity Pieces;
        public Entity Stub;
        public Entity Heap;

        public readonly bool HasPieces => Pieces != Entity.Null;

        /// <summary>Adds every model baked as a prefab but the holed ones, which are <see cref="SectionHole"/>s.</summary>
        public readonly void AddPrefabs(NativeHashSet<Entity> prefabs)
        {
            foreach (var model in new[] { Broken, Pieces, Stub, Heap })
            {
                if (model != Entity.Null)
                {
                    prefabs.Add(model);
                }
            }
        }
    }
}
