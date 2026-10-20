using System;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation
{
    /// <summary>One pivot a <see cref="MotionRigAuthoring"/> moves, and how it bakes into a <see cref="MotionRigPart"/>.</summary>
    [Serializable]
    public abstract class PartSlot
    {
        [Tooltip("The pivot this slot moves, a child of the rig.")]
        public Transform part;

        /// <summary>Why this slot can't bake on <paramref name="rig"/>, or null when it can.</summary>
        public virtual string Problem(MotionRigAuthoring rig)
        {
            if (part == null)
            {
                return $"A {GetType().Name} has no part.";
            }

            return rig.IsBelow(part) ? null : $"Part '{part.name}' must be a child of '{rig.name}'.";
        }

        /// <summary>The part's runtime entry; directions are measured against the <paramref name="rig"/> root.</summary>
        public abstract MotionRigPart Bake(Transform rig, Entity entity);

        /// <summary>
        /// Bounds of the pivot's own meshes (its LOD children, not child pivots) in <paramref name="space"/>'s frame.
        /// </summary>
        public static Bounds MeshBounds(Transform pivot, Transform space)
        {
            var filters = pivot.Cast<Transform>().Select(child => child.GetComponent<MeshFilter>())
                .Where(filter => filter != null).ToArray();
            if (filters.Length == 0)
            {
                throw new InvalidOperationException($"'{pivot.name}' has no mesh to measure.");
            }

            var bounds = new Bounds(space.InverseTransformPoint(filters[0].transform.position), Vector3.zero);
            foreach (var filter in filters)
            {
                var toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1) * 2f - Vector3.one;
                    bounds.Encapsulate(toSpace.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, sign)));
                }
            }

            return bounds;
        }

        protected MotionRigPart Rest(Entity entity, MotionPartKind kind) => new()
        {
            Part = entity,
            Kind = kind,
            RestRotation = part.localRotation,
            RestPosition = part.localPosition,
        };
    }
}
