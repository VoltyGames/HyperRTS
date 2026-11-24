using System;
using HyperRTS.Simulation.Common.Geometry;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>A holed variant of a section's wall or roof, and where its hole is, in the building's space.</summary>
    public struct SectionHole
    {
        public Entity Model;
        public float3 Center;
    }

    /// <summary>
    /// The models that show a <see cref="Section"/>, at the same index: standing, holed, breaking and broken. All but
    /// <see cref="Intact"/> bake as entity prefabs, copied in while they show.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SectionModel : IBufferElementData
    {
        public const int MostHoles = 3;

        public Entity Intact;
        public Entity Broken;
        public Entity Pieces;
        public Entity Stub;
        public Entity Heap;
        public SectionHole FirstHole;
        public SectionHole SecondHole;
        public SectionHole ThirdHole;

        public readonly bool HasPieces => Pieces != Entity.Null;

        public readonly SectionHole Hole(int index) => index switch
        {
            0 => FirstHole,
            1 => SecondHole,
            _ => ThirdHole,
        };

        /// <summary>The hole variant whose hole is nearest <paramref name="local"/>, or -1 when the section has none.</summary>
        public readonly int NearestHole(float3 local)
        {
            var nearest = Closest.None;
            for (var i = 0; i < MostHoles; i++)
            {
                var hole = Hole(i);
                if (hole.Model != Entity.Null)
                {
                    nearest.Consider(i, Entity.Null, math.distancesq(hole.Center, local));
                }
            }

            return nearest.Index;
        }

        /// <summary>Calls <paramref name="add"/> with every model baked as a prefab, everything but <see cref="Intact"/>.</summary>
        public readonly void ForEachPrefab(Action<Entity> add)
        {
            Entity[] prefabs = { Broken, Pieces, Stub, Heap, FirstHole.Model, SecondHole.Model, ThirdHole.Model };
            foreach (var model in prefabs)
            {
                if (model != Entity.Null)
                {
                    add(model);
                }
            }
        }
    }
}
