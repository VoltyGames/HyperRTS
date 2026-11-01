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

    /// <summary>
    /// A piece of a <see cref="NavArea"/>, such as one span of a bridge deck, in its space. An area with parts stamps
    /// only the open ones, so a broken span returns its cells to what lay below.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct NavAreaPart : IBufferElementData
    {
        public float2 Center;
        public float2 Size;
        public bool Open;

        public readonly NavBox Box(in LocalTransform transform) => NavBox.FromPart(transform, Center, Size);
    }

    /// <summary>XZ box, turned with its entity, that pathing and building placement treat as solid.</summary>
    public struct NavObstacle : IComponentData
    {
        public float2 Size;

        /// <summary>A hash of the footprint stamped at <paramref name="transform"/>, to tell when it changed.</summary>
        public readonly ulong Hash(in LocalTransform transform) => NavBox.Hash(transform, this);

        /// <summary>The turned box of every obstacle <paramref name="query"/> matches, which reads LocalTransform too.</summary>
        public static NativeList<NavBox> ReadBoxes(EntityQuery query)
        {
            var obstacles = query.ToComponentDataArray<NavObstacle>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var boxes = new NativeList<NavBox>(obstacles.Length, Allocator.Temp);
            for (var i = 0; i < obstacles.Length; i++)
            {
                boxes.Add(new NavBox(transforms[i].Position, transforms[i].Rotation, obstacles[i].Size));
            }

            return boxes;
        }
    }

    /// <summary>
    /// A piece of a <see cref="NavObstacle"/>, such as one wall of a building, in its space. An obstacle with parts
    /// stamps only the closed ones, so a broken wall opens its cells; placement still uses the whole box.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct NavObstaclePart : IBufferElementData
    {
        public float2 Center;
        public float2 Size;
        public bool Closed;

        public readonly NavBox Box(in LocalTransform transform) => NavBox.FromPart(transform, Center, Size);
    }
}
