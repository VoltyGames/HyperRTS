using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>A direct child of falling remains and its resting pose, turned with the fall about the stump.</summary>
    [InternalBufferCapacity(2)]
    public struct FallPart : IBufferElementData
    {
        public Entity Part;
        public LocalTransform Rest;

        /// <summary>The rest pose turned by <paramref name="turn"/> about the remains' origin.</summary>
        public readonly LocalTransform Turned(quaternion turn) =>
            LocalTransform.FromPositionRotationScale(math.rotate(turn, Rest.Position), math.mul(turn, Rest.Rotation), Rest.Scale);
    }
}
