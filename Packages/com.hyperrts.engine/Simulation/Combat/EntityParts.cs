using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// The entities one object is made of, to destroy it whole. Instantiated prefabs list theirs in
    /// <see cref="LinkedEntityGroup"/>, which destroying the root takes along; SubScene objects have no group, so
    /// their transform children (models, damage stages) are listed here too.
    /// </summary>
    public static class EntityParts
    {
        public static void Collect(Entity root, in BufferLookup<LinkedEntityGroup> groups, in BufferLookup<Child> children,
            NativeList<Entity> parts)
        {
            var start = parts.Length;
            parts.Add(root);
            for (var i = start; i < parts.Length; i++)
            {
                // A group's members go with it, so listing them again would destroy them twice.
                var part = parts[i];
                if (groups.HasBuffer(part) || !children.TryGetBuffer(part, out var kids))
                {
                    continue;
                }

                foreach (var kid in kids)
                {
                    parts.Add(kid.Value);
                }
            }
        }
    }
}
