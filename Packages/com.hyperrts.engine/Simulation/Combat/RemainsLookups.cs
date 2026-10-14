using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>The read-only lookups <see cref="Remains.FromPiece"/> takes a destroyed piece's stretch and fall from.</summary>
    public struct RemainsLookups
    {
        [ReadOnly] public ComponentLookup<PostTransformMatrix> Stretches;
        [ReadOnly] public ComponentLookup<PartStretch> PartStretches;
        [ReadOnly] public ComponentLookup<Topple> Topples;

        public static RemainsLookups Create(ref SystemState state) => new()
        {
            Stretches = state.GetComponentLookup<PostTransformMatrix>(true),
            PartStretches = state.GetComponentLookup<PartStretch>(true),
            Topples = state.GetComponentLookup<Topple>(true),
        };

        public void Update(ref SystemState state)
        {
            Stretches.Update(ref state);
            PartStretches.Update(ref state);
            Topples.Update(ref state);
        }
    }
}
