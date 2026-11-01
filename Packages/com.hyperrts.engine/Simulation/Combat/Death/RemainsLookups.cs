using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Death
{
    /// <summary>The read-only lookups a destroyed piece's remains take their stretch and fall from, and the one place they spawn.</summary>
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

        /// <summary>
        /// Spawns <paramref name="prefab"/> as the remains of <paramref name="piece"/>, which stood at
        /// <paramref name="standing"/>; <paramref name="fall"/> is read only for <see cref="Topple"/> pieces.
        /// </summary>
        public Entity Spawn(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity piece, Entity prefab,
            in LocalTransform standing, byte fall, bool witnessed)
        {
            var remains = Remains.FromPiece(piece, standing, fall, witnessed, this);
            var spawned = ecb.Instantiate(sortKey, prefab);
            ecb.Place(sortKey, spawned, remains.Transform, remains.StretchMatrix);
            if (remains.Stretched)
            {
                ecb.AddComponent(sortKey, spawned, remains.Stretch);
            }

            if (remains.PartsStretched)
            {
                ecb.AddComponent(sortKey, spawned, remains.Parts);
            }

            if (remains.Felled)
            {
                ecb.AddComponent(sortKey, spawned, remains.ToppleFall);
            }

            return spawned;
        }

        public void Update(ref SystemState state)
        {
            Stretches.Update(ref state);
            PartStretches.Update(ref state);
            Topples.Update(ref state);
        }
    }
}
