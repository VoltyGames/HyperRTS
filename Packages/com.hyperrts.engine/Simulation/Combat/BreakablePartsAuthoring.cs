using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Wall and roof sections that swap to their broken model one at a time as health drops; purely visual.</summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Breakable Parts")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class BreakablePartsAuthoring : AuthoringBehaviour
    {
        [Serializable]
        public struct Part
        {
            [Tooltip("Shown while the section stands.")]
            public GameObject intact;

            [Tooltip("Shown once it breaks: the holed wall or roof with its rubble. Never inside the intact model.")]
            public GameObject broken;
        }

        [Tooltip("Share of the parts broken at zero health; the rest stand until the ruin replaces the building.")]
        [Range(0f, 1f)]
        public float breakShare = 0.8f;

        [Tooltip("Effect rig played at a part as it breaks, +Z pointing out of the building (debris, dust).")]
        public GameObject effect;

        [Tooltip("The sections that break. Find Parts By Name in the context menu fills it from X_Intact and X_Broken children.")]
        public Part[] parts = Array.Empty<Part>();

        /// <summary>
        /// Lists every <c>X_Intact</c> child with its <c>X_Broken</c> sibling, hidden at scale 0; a missing sibling
        /// stays empty for validation to report.
        /// </summary>
        [ContextMenu("Find Parts By Name")]
        public void FindPartsByName()
        {
            var found = new List<Part>();
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.EndsWith("_Intact", StringComparison.Ordinal))
                {
                    continue;
                }

                var broken = child.parent.Find(child.name[..^"_Intact".Length] + "_Broken");
                if (broken != null)
                {
                    Record(broken);
                    broken.localScale = Vector3.zero;
                }

                found.Add(new Part { intact = child.gameObject, broken = broken != null ? broken.gameObject : null });
            }

            Record(this);
            parts = found.ToArray();
        }

        /// <summary>What stops the parts baking; empty when they are fine.</summary>
        public List<string> Problems()
        {
            var problems = new List<string>();
            if (parts.Length == 0)
            {
                problems.Add($"Breakable parts on '{name}' list no parts.");
            }

            for (var i = 0; i < parts.Length; i++)
            {
                var problem = PartProblem(parts[i], i);
                if (problem != null)
                {
                    problems.Add(problem);
                }
            }

            return problems;
        }

        /// <summary>The part's centre in the building's space, and the way debris flies: out from the middle.</summary>
        public (Vector3 Centre, Vector3 Outward) Placement(Part part)
        {
            var centre = LocalBounds(part.intact.transform).center;
            var offset = centre - LocalBounds(transform).center;
            offset.y = 0f;
            return (centre, offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.up);
        }

        private string PartProblem(Part part, int index)
        {
            if (part.intact == null || part.broken == null)
            {
                return $"Breakable part {index} on '{name}' needs both an intact and a broken model.";
            }

            if (!part.intact.transform.IsChildOf(transform) || !part.broken.transform.IsChildOf(transform))
            {
                return $"Breakable part {index} on '{name}' uses a model from outside the building.";
            }

            if (part.broken.transform.IsChildOf(part.intact.transform))
            {
                return $"Breakable part '{part.broken.name}' on '{name}' sits inside '{part.intact.name}' and would hide with it.";
            }

            // Hidden in the prefab so editor views and map captures show the standing building.
            return part.broken.transform.localScale == Vector3.zero
                ? null
                : $"Breakable part '{part.broken.name}' on '{name}' should start hidden at scale 0.";
        }

        private static void Record(Object target)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(target, "Find Parts By Name");
#endif
        }

        private Bounds LocalBounds(Transform node)
        {
            var bounds = new Bounds();
            var any = false;
            foreach (var filter in node.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var toRoot = transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    var point = toRoot.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, sign));
                    if (any)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                }
            }

            if (!any)
            {
                throw new InvalidOperationException(
                    $"HyperRTS: breakable parts on '{name}': '{node.name}' has no mesh to measure.");
            }

            return bounds;
        }

        public class Baker : Baker<BreakablePartsAuthoring>
        {
            public override void Bake(BreakablePartsAuthoring authoring)
            {
                var problems = authoring.Problems();
                if (problems.Count > 0)
                {
                    throw new InvalidOperationException("HyperRTS: " + problems[0]);
                }

                // Through the baker, so moves and mesh edits rebake.
                GetComponentsInChildren<Transform>();
                foreach (var filter in GetComponentsInChildren<MeshFilter>())
                {
                    DependsOn(filter.sharedMesh);
                }

                var parts = new List<BreakablePart>(authoring.parts.Length);
                foreach (var part in authoring.parts)
                {
                    var (centre, outward) = authoring.Placement(part);
                    parts.Add(new BreakablePart
                    {
                        Intact = GetEntity(part.intact, TransformUsageFlags.Dynamic),
                        Broken = GetEntity(part.broken, TransformUsageFlags.Dynamic),
                        Centre = centre,
                        Outward = outward,
                    });
                }

                var bounds = authoring.LocalBounds(authoring.transform);
                var writer = new BakerWriter(this, GetEntity(TransformUsageFlags.Dynamic));
                writer.AddBuffer(parts);
                writer.Add(new BreakableParts
                {
                    BreakShare = authoring.breakShare,
                    Effect = authoring.effect,
                    BoundsCentre = bounds.center,
                    BoundsExtents = bounds.extents,
                });
                writer.Add(new ShownBreakage());
            }
        }
    }
}
