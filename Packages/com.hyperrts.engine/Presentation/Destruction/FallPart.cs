using Unity.Collections;
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

        /// <summary>Adds each of <paramref name="group"/> that is a direct child of <paramref name="remains"/>, at rest.</summary>
        public static void Record(Entity remains, NativeList<Entity> group, in ComponentLookup<Parent> parents,
            in ComponentLookup<LocalTransform> transforms, DynamicBuffer<FallPart> parts)
        {
            foreach (var member in group)
            {
                if (parents.TryGetComponent(member, out var parent) && parent.Value == remains)
                {
                    parts.Add(new FallPart { Part = member, Rest = transforms[member] });
                }
            }
        }
    }
}
