using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// An entity that shows one of several child models, picked from where it stands so a row of them (a wall) doesn't
    /// repeat one look, and every client picks the same.
    /// </summary>
    public struct ModelVariants : IComponentData
    {
        public int Count;

        /// <summary>The variant on screen; -1 until presentation picks one.</summary>
        public int Shown;

        /// <summary>
        /// Hashes the half-metre cell, so replication's rounding can't change the pick. <c>math.hash</c> is linear in
        /// each coordinate, so evenly spaced cells can share a remainder; the index random mixes it first.
        /// </summary>
        public readonly int Pick(float3 position)
        {
            var cell = (int2)math.round(position.xz * 2f);
            return Random.CreateFromIndex(math.hash(cell)).NextInt(Count);
        }
    }

    /// <summary>Set once presentation has picked the <see cref="ModelVariants"/> model; baked off.</summary>
    public struct ModelVariantPicked : IComponentData, IEnableableComponent
    {
    }

    /// <summary>A child model shown only while <see cref="Variant"/> is the picked one.</summary>
    [InternalBufferCapacity(0)]
    public struct ModelVariantPart : IBufferElementData
    {
        public Entity Visual;
        public int Variant;
    }
}
