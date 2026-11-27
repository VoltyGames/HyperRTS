using System;
using HyperRTS.Simulation.Common.Geometry;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs;

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

    /// <summary>Bounds of the part's own mesh (not its child parts) in <paramref name="space"/>'s frame.</summary>
    public static Bounds MeshBounds(Transform part, Transform space)
    {
        if (!part.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
        {
            throw new InvalidOperationException($"'{part.name}' has no mesh to measure: a part carries its own mesh.");
        }

        return BoundsMath.Moved(filter.sharedMesh.bounds, space.worldToLocalMatrix * part.localToWorldMatrix);
    }

    protected virtual MotionRigPart AtRest(Entity entity, MotionPartKind kind) => new()
    {
        Part = entity,
        Kind = kind,
        RestRotation = part.localRotation,
        RestPosition = part.localPosition,
    };
}
