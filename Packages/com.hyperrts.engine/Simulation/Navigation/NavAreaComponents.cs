using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Axis-aligned XZ box stamped over the grid's terrain surfaces, before <see cref="NavObstacle"/>s.</summary>
    public struct NavArea : IComponentData
    {
        public float2 Size;
        public NavAreaKind Kind;
    }

    /// <summary>How a <see cref="NavArea"/> overrides the grid cells under it.</summary>
    public enum NavAreaKind : byte
    {
        /// <summary>Water for every layer's purposes: naval agents pass, ground agents don't.</summary>
        Water = 0,

        /// <summary>Closed to every layer.</summary>
        Blocked = 1,

        /// <summary>Walkable at the area's height (a bridge); water below stays open to ships.</summary>
        Deck = 2,
    }

    /// <summary>XZ box, turned with its entity, that pathing and building placement treat as solid.</summary>
    public struct NavObstacle : IComponentData
    {
        public float2 Size;

        /// <summary>A hash of the footprint stamped at <paramref name="transform"/>, to tell when it changed.</summary>
        public readonly ulong Hash(in LocalTransform transform)
        {
            var hash = xxHash3.Hash64(transform) ^ xxHash3.Hash64(this).yx;
            return ((ulong)hash.x << 32) | hash.y;
        }
    }
}
