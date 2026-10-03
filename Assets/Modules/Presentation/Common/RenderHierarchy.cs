using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Common
{
    /// <summary>Finds every entity that may carry a mesh for a gameplay root, so render tweaks reach child meshes.</summary>
    public static class RenderHierarchy
    {
        /// <summary>
        /// Instantiated prefabs list their parts in <see cref="LinkedEntityGroup"/> (their <see cref="Child"/> buffer
        /// appears a frame later); SubScene objects have no group, so walk the transform children instead.
        /// </summary>
        public static void Collect(Entity root, in BufferLookup<LinkedEntityGroup> linked, in BufferLookup<Child> children,
            ref FixedList512Bytes<Entity> result)
        {
            result.Clear();
            if (linked.TryGetBuffer(root, out var group))
            {
                for (var i = 0; i < group.Length && result.Length < result.Capacity; i++)
                {
                    result.Add(group[i].Value);
                }

                return;
            }

            result.Add(root);
            for (var i = 0; i < result.Length; i++)
            {
                if (!children.TryGetBuffer(result[i], out var kids))
                {
                    continue;
                }

                for (var k = 0; k < kids.Length && result.Length < result.Capacity; k++)
                {
                    result.Add(kids[k].Value);
                }
            }
        }
    }
}
